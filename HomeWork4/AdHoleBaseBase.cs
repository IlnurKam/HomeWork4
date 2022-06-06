using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using static HomeWork4.AdHole;

namespace HomeWork4
{
    [Transaction(TransactionMode.Manual)]
    public class AdHoleBaseBase
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document kjDoc = commandData.Application.ActiveUIDocument.Document;
            Document ovDoc = kjDoc.Application.Documents.OfType<Document>()
                .Where(x => x.Title.Contains("ОВ"))
                .FirstOrDefault();
            if (ovDoc == null)
            {
                TaskDialog.Show("Ошибка", "Не найден файл ОВ");
                return Result.Cancelled;
            }

            FamilySymbol familySymbol = new FilteredElementCollector(kjDoc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .OfType<FamilySymbol>()
                .Where(x => x.FamilyName.Equals("Отверстия"))
                .FirstOrDefault();
            if (familySymbol == null)
            {
                TaskDialog.Show("Ошибка", "Не найден семейство \"Отверстия\"");
                return Result.Cancelled;
            }

            List<Duct> ducts = new FilteredElementCollector(ovDoc)
                .OfClass(typeof(Duct))
                .OfType<Duct>()
                .ToList();

            View3D view3D = new FilteredElementCollector(kjDoc)
                .OfClass(typeof(View3D))
                .OfType<View3D>()
                .Where(x => !x.IsTemplate)
                .FirstOrDefault();
            if (view3D == null)
            {
                TaskDialog.Show("Ошибка", "Не найден 3х мерный вид");
                return Result.Cancelled;
            }

            ReferenceIntersector referenceIntersector = new ReferenceIntersector(new ElementClassFilter(typeof(Wall)), FindReferenceTarget.Element, view3D);

            Transaction transaction0 = new Transaction(kjDoc);
            transaction0.Start("Расстановка отверстий");

            if (!familySymbol.IsActive)
            {
                familySymbol.Activate();
            }
            transaction0.Commit();

            Transaction transaction = new Transaction(kjDoc);
            transaction.Start("Расстановка отверстий");

            foreach (Duct d in ducts)
            {
                Line curve = (d.Location as LocationCurve).Curve as Line;
                XYZ point = curve.GetEndPoint(0);
                XYZ direction = curve.Direction;

                List<ReferenceWithContext> intersections = referenceIntersector.Find(point, direction)
                    .Where(x => x.Proximity <= curve.Length)
                    .Distinct(new ReferenceWithContextElementEqualityComparer())
                    .ToList();
                foreach (ReferenceWithContext refer in intersections)
                {
                    double proximity = refer.Proximity;
                    Reference reference = refer.GetReference();
                    Wall wall = kjDoc.GetElement(reference.ElementId) as Wall;
                    Level level = kjDoc.GetElement(wall.LevelId) as Level;
                    XYZ pointHole = point + (direction * proximity);

                    FamilyInstance hole = kjDoc.Create.NewFamilyInstance(pointHole, familySymbol, wall, level, StructuralType.NonStructural);
                    Parameter width = hole.LookupParameter("Ширина");
                    Parameter height = hole.LookupParameter("Высота");
                    width.Set(d.Diameter);
                    height.Set(d.Diameter);
                }
            }
            transaction.Commit();
            return Result.Succeeded;

        }
        public Result Exeute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            var collector = new FilteredElementCollector(doc);

            var pipe = (Pipe)collector
                    .OfClass(typeof(Pipe))
                    .FirstElement();

            var pipeLocation = (LocationCurve)pipe.Location;

            var pipeLine = (Line)pipeLocation.Curve;

            var referenceIntersector = new ReferenceIntersector(new ElementClassFilter(typeof(Wall)), FindReferenceTarget.Element, (View3D)uidoc.ActiveGraphicalView)
            {
                FindReferencesInRevitLinks = true
            };

            var origin = pipeLine.GetEndPoint(0);

            var intersections = referenceIntersector.Find(origin, pipeLine.Direction)
                    .Where(x => x.Proximity <= pipeLine.Length)
                    .Distinct(new ReferenceWithContextElementEqualityComparer())
                    .ToList();

            TaskDialog.Show("dev", $"Found {intersections.Count} intersections");

            return Result.Succeeded;
        }
    }
}