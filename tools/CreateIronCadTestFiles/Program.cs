using System;
using System.IO;

namespace CreateIronCadTestFiles
{
    class Program
    {
        static void CreateBoxStl(string filePath, string name, double sx, double sy, double sz)
        {
            double hx = sx / 2, hy = sy / 2, hz = sz / 2;
            using (var w = new StreamWriter(filePath))
            {
                w.WriteLine($"solid {name}");

                // 6 faces, 2 triangles each = 12 triangle definitions
                // Each triangle: normal (nx,ny,nz), v0 (x,y,z), v1 (x,y,z), v2 (x,y,z)
                double[][][] tris = {
                    // +Z
                    new double[][] { new[] {0.0,0.0,1.0}, new[] {-hx,-hy,hz}, new[] {hx,-hy,hz}, new[] {hx,hy,hz} },
                    new double[][] { new[] {0.0,0.0,1.0}, new[] {-hx,-hy,hz}, new[] {hx,hy,hz}, new[] {-hx,hy,hz} },
                    // -Z
                    new double[][] { new[] {0.0,0.0,-1.0}, new[] {-hx,-hy,-hz}, new[] {-hx,hy,-hz}, new[] {hx,hy,-hz} },
                    new double[][] { new[] {0.0,0.0,-1.0}, new[] {-hx,-hy,-hz}, new[] {hx,hy,-hz}, new[] {hx,-hy,-hz} },
                    // +X
                    new double[][] { new[] {1.0,0.0,0.0}, new[] {hx,-hy,-hz}, new[] {hx,hy,-hz}, new[] {hx,hy,hz} },
                    new double[][] { new[] {1.0,0.0,0.0}, new[] {hx,-hy,-hz}, new[] {hx,hy,hz}, new[] {hx,-hy,hz} },
                    // -X
                    new double[][] { new[] {-1.0,0.0,0.0}, new[] {-hx,-hy,-hz}, new[] {-hx,-hy,hz}, new[] {-hx,hy,hz} },
                    new double[][] { new[] {-1.0,0.0,0.0}, new[] {-hx,-hy,-hz}, new[] {-hx,hy,hz}, new[] {-hx,hy,-hz} },
                    // +Y
                    new double[][] { new[] {0.0,1.0,0.0}, new[] {-hx,hy,-hz}, new[] {-hx,hy,hz}, new[] {hx,hy,hz} },
                    new double[][] { new[] {0.0,1.0,0.0}, new[] {-hx,hy,-hz}, new[] {hx,hy,hz}, new[] {hx,hy,-hz} },
                    // -Y
                    new double[][] { new[] {0.0,-1.0,0.0}, new[] {-hx,-hy,-hz}, new[] {hx,-hy,-hz}, new[] {hx,-hy,hz} },
                    new double[][] { new[] {0.0,-1.0,0.0}, new[] {-hx,-hy,-hz}, new[] {hx,-hy,hz}, new[] {-hx,-hy,hz} },
                };

                foreach (var t in tris)
                {
                    var n = t[0]; var v0 = t[1]; var v1 = t[2]; var v2 = t[3];
                    w.WriteLine($"  facet normal {n[0]:F6} {n[1]:F6} {n[2]:F6}");
                    w.WriteLine("    outer loop");
                    w.WriteLine($"      vertex {v0[0]:F6} {v0[1]:F6} {v0[2]:F6}");
                    w.WriteLine($"      vertex {v1[0]:F6} {v1[1]:F6} {v1[2]:F6}");
                    w.WriteLine($"      vertex {v2[0]:F6} {v2[1]:F6} {v2[2]:F6}");
                    w.WriteLine("    endloop");
                    w.WriteLine("  endfacet");
                }
                w.WriteLine($"endsolid {name}");
            }
        }

        static object Invoke(object obj, string method, params object[] args)
        {
            try
            {
                return obj.GetType().InvokeMember(method,
                    System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null, obj, args);
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        static object Get(object obj, string prop)
        {
            return obj.GetType().InvokeMember(prop,
                System.Reflection.BindingFlags.GetProperty | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, obj, null);
        }

        static void Set(object obj, string prop, object value)
        {
            obj.GetType().InvokeMember(prop,
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, obj, new[] { value });
        }

        [STAThread]
        static void Main(string[] args)
        {
            string outputDir = args.Length > 0
                ? args[0]
                : @"C:\Users\TD-999\Research\ArasInnovator\copilot-worktrees\StudyCase_0603\IRONCASE";

            Directory.CreateDirectory(outputDir);

            string[] partNames = {
                "BasePlate",     // 001
                "SidePanel",     // 002
                "MotorMount",    // 003
                "Gearbox",       // 004
                "BeltPulley",    // 005
                "PCBBracket",    // 006
                "WiringDuct",    // 007
                "ValveBlock",    // 008
                "Cylinder",      // 009
                "HoseConnector", // 010
                "SensorMount",   // 011
                "CoverLid"       // 012
            };

            string tempDir = Path.Combine(Path.GetTempPath(), "IdeaCadStl_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            Console.Write("Starting IronCAD...");
            Type icType = Type.GetTypeFromProgID("IronCAD.Application");
            object icApp = Activator.CreateInstance(icType);
            Set(icApp, "visible", false);
            Console.WriteLine(" OK.");

            try
            {
                // Step 1: Create STL files and import into IronCAD, save as .ics
                for (int i = 0; i < partNames.Length; i++)
                {
                    int seq = i + 1;
                    string icsFileName = $"IRONCASE_Ver1.0_{seq:D3}.ics";
                    string icsFilePath = Path.Combine(outputDir, icsFileName);

                    if (File.Exists(icsFilePath))
                    {
                        Console.WriteLine($"  {icsFileName} already exists, skipping.");
                        continue;
                    }

                    Console.Write($"  Creating {icsFileName} ({partNames[i]})...");

                    // Create STL
                    double sx = 0.08 * (seq + 2);
                    double sy = 0.06 * (seq + 2);
                    double sz = 0.02 * (seq + 3);
                    string stlPath = Path.Combine(tempDir, $"{partNames[i]}.stl");
                    CreateBoxStl(stlPath, partNames[i], sx, sy, sz);

                    // Create a new blank page
                    object pages = Get(icApp, "Pages");
                    object page = Invoke(pages, "Add", Type.Missing, Type.Missing);

                    // Import STL into the page
                    object imported = Invoke(page, "ImportFile", stlPath);

                    if (imported != null)
                    {
                        // Try to set name of imported shape
                        object shape = Get(page, "Shape");
                        if (shape != null) Set(shape, "Name", partNames[i]);
                    }

                    // Save as .ics
                    Invoke(page, "SaveAs", icsFilePath);
                    Invoke(page, "Close");
                    // Remove the page from Pages collection
                    try { Invoke(pages, "Remove", page); } catch { }

                    Console.WriteLine(" OK.");
                }

                // Step 2: Create assembly by importing all .ics files
                string assemblyFileName = "Assembly-IRONCASE-Ver1.0A.ics";
                string assemblyPath = Path.Combine(outputDir, assemblyFileName);

                if (File.Exists(assemblyPath))
                {
                    Console.WriteLine($"  {assemblyFileName} already exists, skipping.");
                }
                else
                {
                    Console.Write($"  Creating {assemblyFileName}...");

                    object pages = Get(icApp, "Pages");
                    object asmPage = Invoke(pages, "Add", assemblyPath, true);
                    if (asmPage == null) asmPage = Get(icApp, "ActivePage");

                    for (int i = 0; i < partNames.Length; i++)
                    {
                        int seq = i + 1;
                        string detailFileName = $"IRONCASE_Ver1.0_{seq:D3}.ics";
                        string detailPath = Path.Combine(outputDir, detailFileName);

                        if (!File.Exists(detailPath))
                        {
                            Console.WriteLine($"\n  WARNING: {detailFileName} not found, skipping.");
                            continue;
                        }

                        try
                        {
                            object added = Invoke(Get(asmPage, "Shapes"), "Add", detailPath);
                            if (added != null)
                            {
                                try { Set(added, "Name", partNames[i]); } catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"\n  Import of {detailFileName} failed: {ex.Message}");
                        }
                    }

                    Invoke(asmPage, "SaveAs", assemblyPath);
                    Invoke(asmPage, "Close");
                    try { Invoke(pages, "Remove", asmPage); } catch { }

                    Console.WriteLine(" OK.");
                }

                Console.WriteLine("Done. All files created successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("\nPress Enter to exit...");
                Console.ReadLine();
            }
            finally
            {
                try
                {
                    Invoke(icApp, "Quit");
                }
                catch { }
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
