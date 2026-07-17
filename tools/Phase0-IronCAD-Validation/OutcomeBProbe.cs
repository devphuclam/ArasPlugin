using System;
using System.IO;
using interop.ICApiIronCAD;

internal static class OutcomeBProbe
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: OutcomeBProbe.exe <child.ics> <root-output.ics>");
            return 2;
        }

        IZBaseApp app = null;
        IZSceneDoc scene = null;
        try
        {
            var appType = Type.GetTypeFromProgID("IronCAD.Application");
            if (appType == null) throw new InvalidOperationException("IRONCAD_PROGID_NOT_FOUND");
            dynamic dynamicApp = Activator.CreateInstance(appType);
            app = (IZBaseApp)dynamicApp;
            dynamicApp.Visible = false;

            dynamic dynamicScene = dynamicApp.Pages.Add(Type.Missing, Type.Missing);
            scene = (IZSceneDoc)dynamicScene;
            object added;
            try { added = dynamicScene.Shapes.Add(Path.GetFullPath(args[0])); }
            catch { added = dynamicScene.ImportFile(Path.GetFullPath(args[0]), true); }

            var output = Path.GetFullPath(args[1]);
            scene.SaveAs(output, eZLinksSaveOptions.Z_LINKS_IGNORE, true);

            string link = null;
            try { link = ((dynamic)added).ModelLinkPath; } catch { }
            Console.WriteLine("ROOT_EXISTS={0}", File.Exists(output));
            Console.WriteLine("CHILD_LINK={0}", link ?? string.Empty);
            Console.WriteLine("CHILD_EXISTS={0}", File.Exists(Path.GetFullPath(args[0])));
            return File.Exists(output) && !string.IsNullOrWhiteSpace(link) ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR={0}", ex);
            return 1;
        }
        finally
        {
            try { if (scene != null) ((dynamic)scene).Close(); } catch { }
            try { if (app != null) ((dynamic)app).Quit(); } catch { }
        }
    }
}
