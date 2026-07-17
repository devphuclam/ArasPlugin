using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using interop.ICApiIronCAD;

internal static class Phase0Probe
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: Phase0Probe.exe <source.ics> <output-directory> [--open]");
            return 2;
        }

        var sourcePath = Path.GetFullPath(args[0]);
        var outputDirectory = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(outputDirectory);

        IZBaseApp app = null;
        IZDoc opened = null;
        try
        {
            app = GetRunningApplication();
            if (args.Any(a => string.Equals(a, "--open", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("OPEN_MODE=explicit");
                opened = app.OpenFile(sourcePath, false);
            }
            else
            {
                Console.WriteLine("OPEN_MODE=active-document");
                opened = app.ActiveDoc;
                if (opened == null)
                    throw new InvalidOperationException("NO_ACTIVE_DOCUMENT; open the sample in IronCAD, then rerun or pass --open explicitly.");
            }
            var scene = opened as IZSceneDoc;
            if (scene == null)
                throw new InvalidOperationException("SOURCE_IS_NOT_SCENE");

            var top = scene.GetTopElement();
            var elements = Flatten(top).ToList();
            var saveNonePath = Path.Combine(outputDirectory, "save-none.ics");
            scene.SaveAsCopy(saveNonePath, eZLinksSaveOptions.Z_LINKS_IGNORE, true);

            Console.WriteLine("SOURCE={0}", sourcePath);
            Console.WriteLine("TOP_PRESENT={0}", top != null);
            Console.WriteLine("ELEMENT_COUNT={0}", elements.Count);
            Console.WriteLine("SAVE_NONE_EXISTS={0}", File.Exists(saveNonePath));
            Console.WriteLine("SAVE_NONE_PATH={0}", saveNonePath);

            foreach (var link in ReadLinks(elements))
                Console.WriteLine("LINK|{0}|{1}", link.Path, link.ModelLinkPath ?? string.Empty);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR={0}", ex);
            return 1;
        }
        finally
        {
            if (app != null && opened != null)
            {
                try { app.CloseFile(opened); } catch { }
            }
        }
    }

    private static IZBaseApp GetRunningApplication()
    {
        try
        {
            return (IZBaseApp)Marshal.GetActiveObject("IronCAD.Application");
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException(
                "IRONCAD_NOT_REGISTERED_IN_ROT; open the sample in the visible IronCAD instance before running the probe.", ex);
        }
    }

    private static IEnumerable<IZElement> Flatten(IZElement element)
    {
        if (element == null) yield break;
        yield return element;

        IZArray children;
        try { children = element.GetChildrenZArray(); }
        catch { yield break; }
        if (children == null) yield break;

        int count;
        children.Count(out count);
        for (var i = 0; i < count; i++)
        {
            object value;
            IZElement child = null;
            try { children.Get(i, out value); child = value as IZElement; } catch { }
            foreach (var nested in Flatten(child)) yield return nested;
        }
    }

    private sealed class LinkInfo
    {
        public string Path { get; set; }
        public string ModelLinkPath { get; set; }
    }

    private static IEnumerable<LinkInfo> ReadLinks(IList<IZElement> elements)
    {
        for (var i = 0; i < elements.Count; i++)
        {
            var sceneElement = elements[i] as IZSceneElement;
            if (sceneElement == null) continue;
            string link = null;
            try { link = sceneElement.ModelLinkPath; } catch { }
            yield return new LinkInfo { Path = i.ToString(), ModelLinkPath = link };
        }
    }
}
