using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
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
            double dz = UnitUtils.ConvertToInternalUnits(1500, UnitTypeId.Millimeters);

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
            AddDoor(doc, level1, walls[0]);
            AddWindow(doc, level1, walls[1]);
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);

            //два варианта создания кровли
            //AddRoof(doc, level2, walls);
            AddRoofExtrusion(doc, level2, dy, dx, dz);
            transaction.Commit();

            return walls;
        }

        private void AddRoofExtrusion(Document doc, Level level2, double dy, double dx, double dz)
        {
            ElementId id = doc.GetDefaultElementTypeId(ElementTypeGroup.RoofType);
            RoofType type = doc.GetElement(id) as RoofType;
            if (type == null)
            {
                TaskDialog.Show("Error", "Not RoofType");                
                return;
            }
            //создание профиля
            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(0, -dy, 14.3), new XYZ(0, 0, (dz + 14.3))));
            curveArray.Append(Line.CreateBound(new XYZ(0, 0, (dz + 14.3)), new XYZ(0, dy, 14.3)));

            //создание опорной плоскости
            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(-dx, -dy, dz), new XYZ(-dx, -dy, (dz + dz / 2)), new XYZ(0, -1, 0), doc.ActiveView);
            doc.Create.NewExtrusionRoof(curveArray, plane, level2, type, 0, -4 * dy);

        }

        private void AddRoof(Document doc, Level level2, List<Wall> walls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2 + 1;
            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dt, -dt, 0));
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));
            points.Add(new XYZ(-dt, -dt, 0));

            Application application = doc.Application;
            CurveArray footprint = application.Create.NewCurveArray();
            for (int i = 0; i < 4; i++)
            {
                LocationCurve curve = walls[i].Location as LocationCurve;
                //footprint.Append(curve.Curve);
                XYZ p1 = curve.Curve.GetEndPoint(0);
                XYZ p2 = curve.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
                footprint.Append(line);
            }
            ModelCurveArray footPrintModelCurveMapping = new ModelCurveArray();
            FootPrintRoof footPrintRoof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintModelCurveMapping);
            //Заменим итератор на цикл foreach
            //ModelCurveArrayIterator iterator = footPrintModelCurveMapping.ForwardIterator();
            //iterator.Reset();
            //while (iterator.MoveNext())
            //{
            //    ModelCurve modelCurve = iterator.Current as ModelCurve;
            //    footPrintRoof.set_DefinesSlope(modelCurve, true);
            //    footPrintRoof.set_SlopeAngle(modelCurve, 0.5);
            //}
            foreach (ModelCurve m in footPrintModelCurveMapping)
            {
                footPrintRoof.set_DefinesSlope(m, true);
                footPrintRoof.set_SlopeAngle(m, 0.5);
            }
;
        }

        private void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 1830 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!windowType.IsActive)
                windowType.Activate();

            doc.Create.NewFamilyInstance(point, windowType, wall, level1, StructuralType.NonStructural);
        }

        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);

        }
    }
}

#region
//см пример ExtrusionRoof
//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;

//namespace RevitAddin4
//{
//    [TransactionAttribute(TransactionMode.Manual)]
//    public class RevitAddin : IExternalCommand
//    {
//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
//            Document doc = uiDoc.Document;
//            // Obtenga el tipo de techo predeterminado
//            ElementId id = doc.GetDefaultElementTypeId(ElementTypeGroup.RoofType);
//            RoofType type = doc.GetElement(id) as RoofType;
//            if (type == null)
//            {
//                TaskDialog.Show("Error", "Not RoofType");
//                return Result.Failed;
//            }
//            // Crear esquema
//            CurveArray curveArray = new CurveArray();
//            curveArray.Append(Line.CreateBound(new XYZ(0, 0, 0), new XYZ(0, 20, 20)));
//            curveArray.Append(Line.CreateBound(new XYZ(0, 20, 20), new XYZ(0, 40, 0)));
//            // Obtener la elevación de la vista actual
//            Level level = doc.ActiveView.GenLevel;
//            if (level == null)
//            {
//                TaskDialog.Show("Error", "No es PlainView");
//                return Result.Failed;
//            }
//            // Crear techo
//            using (Transaction tr = new Transaction(doc))
//            {
//                tr.Start("Create ExtrusionRoof");
//                ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
//                doc.Create.NewExtrusionRoof(curveArray, plane, level, type, 0, 40);
//                tr.Commit();
//            }
//            return Result.Succeeded;
//        }
//    }
#endregion
