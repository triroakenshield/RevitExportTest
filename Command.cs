using Autodesk.Revit.Attributes;
//
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System.Windows.Forms;

using App = Autodesk.Revit.ApplicationServices;

namespace RevitExportTest
{
    [Transaction(TransactionMode.ReadOnly)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            App.Application app = uiapp.Application;
            Document doc = uidoc.Document;

            if (null == uidoc)
            {
                message = "Please run this command in an active project document.";
                return Result.Failed;
            }

            View3D view = doc.ActiveView as View3D;

            if (null == view)
            {
                message = "Please run this command in a 3D view.";
                return Result.Failed;
            }

            SaveFileDialog sdial = new SaveFileDialog();
            sdial.Filter = "gltf|*.gltf|glb|*.glb";
            if (sdial.ShowDialog() == DialogResult.OK)
            {
                TestExportContext context = new TestExportContext(doc);

                using (CustomExporter exporter = new CustomExporter(doc, context))
                {
                    exporter.IncludeGeometricObjects = false;
                    exporter.Export(view);
                    context._model.SaveGLB(sdial.FileName);
                    context._model.SaveGLTF(sdial.FileName);
                }
            }
            return Result.Succeeded;
        }
    }
}
