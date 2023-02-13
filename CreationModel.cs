using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {            
            Document doc = commandData.Application.ActiveUIDocument.Document;

            List<XYZ> points = new List<XYZ>();
            
            List<Wall> wallCr = CreateWalls(doc, points);            

            return Result.Succeeded;
        }       

        public List<Wall> CreateWalls(Document doc, List<XYZ> points)
        {
            List<Level> listlevel = new FilteredElementCollector(doc)
               .OfClass(typeof(Level))               
               .OfType<Level>()
               .ToList();

            Level level1 = listlevel
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();
            Level level2 = listlevel
               .Where(x => x.Name.Equals("Уровень 2"))
               .FirstOrDefault();

            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
            double dx = width / 2;
            double dy = depth / 2;
            
            List<XYZ> point = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Wall> walls = new List<Wall>();

            Transaction transaction = new Transaction(doc, "Create Wall");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }
            transaction.Commit();

            return walls;
        }
    }
}


