﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ImageResizer.ReleaseBuilder.Classes;

namespace ImageResizer.ReleaseBuilder {
    public class Build :Interaction {
        FolderFinder f = new FolderFinder("Core" );
        Devenv d = null;
        FsQuery q = null;
        VersionEditor v = null;

        public Build() {
            d = new Devenv(Path.Combine(f.folderPath,"ImageResizer.sln"));
            v = new VersionEditor(Path.Combine(f.folderPath, "SharedAssemblyInfo.cs"));

            packages.Add(new PackageDescriptor("min", PackMin));
            packages.Add(new PackageDescriptor("full", PackFull));
            packages.Add(new PackageDescriptor("core", PackCore));
            packages.Add(new PackageDescriptor("standard", PackStandard));

        }

        public string getReleasePath(string packageBase, string ver,  string kind) {
            return Path.Combine(Path.Combine(f.parentPath, "Releases"), packageBase + ver.Trim('-') + '-' + kind + "-" + DateTime.UtcNow.ToString("MMM-dd-yyyy") + ".zip");
        }

        List<PackageDescriptor> packages = new List<PackageDescriptor>();

        public void Run() {
            say("Project root: " + f.parentPath);
            nl();

            string packageBase = v.get("PackageName"); //    // [assembly: PackageName("Resizer")]

            //a. Ask for file version number   [assembly: AssemblyFileVersion("3.0.5.*")]
            string fileVer = change("FileVersion", v.get("AssemblyFileVersion").TrimEnd('.', '*'));
            //b. Ask for assembly version number  AssemblyVersion("3.0.5.*")]
            string assemblyVer = change("AssemblyVersion", v.get("AssemblyVersion").TrimEnd('.', '*'));
            //c: Ask for information version number.  [assembly: AssemblyInformationalVersion("3-alpha-5")]
            string infoVer = change("InfoVersion", v.get("AssemblyInformationalVersion").TrimEnd('.', '*'));

            //d: For each package, specify options: choose 'c' (create and/or overwrite), 'u' (upload), 's' (skip), 'p' (make private). Should inform if the file already exists.
            nl();
            say("For each package, specify all operations to perform, then press enter.");
            say("'c' - Create package (overwrite if exists), 'u' (upload to S3), 's' (skip), 'p' (make private)");
            foreach (PackageDescriptor desc in packages) {
                desc.Path = getReleasePath(packageBase, infoVer, desc.Kind);
                if (desc.Exists) say("\n" + Path.GetFileName(desc.Path) + " already exists");
                string opts = "";

                while(string.IsNullOrEmpty(opts)){
                    Console.Write(desc.Kind + " (" + opts + "):");
                    opts = Console.ReadLine().Trim();
                }
                desc.Options = opts;
                
            }


            //1 - Prompt to Clean
            //1b - Run cleanup routine
            if (ask("Clean All?")) {
                CleanAll();
                RemoveUselessFiles();
            }

            //2 - Set version numbers (with *, if missing)
            string originalContents = v.Contents; //Save for checking changes.
            v.set("AssemblyFileVersion", v.join(fileVer, "*"));
            v.set("AssemblyVersion", v.join(assemblyVer, "*"));
            v.set("AssemblyInformationalVersion", infoVer);
            v.Save();
            //Save contents for reverting later
            string fileContents = v.Contents;
            
            //3 - Prompt to commit and tag
            bool versionsChanged = !fileContents.Equals(originalContents);
            string question = versionsChanged ? "SharedAssemblyInfo.cs was modified. Commit it (and any other changes) to the repository, then hit 'y'."
                : "Are all changes commited? Hit 'y' to continue.";
            while (!ask(question)) { }


            //[assembly: Commit("git-commit-guid-here")]
            //4 - Embed git commit value
            v.set("Commit", "git-commit-todo");
            v.Save();

            //4b - change to hard version number for building
            short revision = (short)(DateTime.UtcNow.TimeOfDay.Milliseconds % short.MaxValue); //the part under 32767. Can actually go up to, 65534, but what's the point.
            v.set("AssemblyFileVersion", v.join(fileVer, revision.ToString()));
            v.set("AssemblyVersion", v.join(assemblyVer, revision.ToString()));
            v.Save();

            //6 - if (c) was specified for any package, build all.
            bool buildOne = false;
            foreach(PackageDescriptor pd in packages) if (pd.Build) buildOne = true;
            
            if (buildOne) BuildAll();

            //7 - Revert file to state at commit (remove 'full' version numbers and 'commit' value)
            v.Contents = fileContents;
            v.Save();


            //8 - run cleanup routine
            RemoveUselessFiles();

            //Prepare searchers
            PrepareForPackaging();

            //9 - Pacakge all selected configurations
            foreach (PackageDescriptor pd in packages) {
                if (pd.Skip) continue;
                if (pd.Exists && pd.Build) {
                    File.Delete(pd.Path);
                    say("Deleted " + pd.Path);
                }
                pd.Builder(pd);
                //Copy to a 'tozip' version for e-mailing
                File.Copy(pd.Path, pd.Path.Replace(".zip", ".tozip"),true);
            }

            //10 - Upload all selected configurations
            foreach (PackageDescriptor pd in packages) {
                if (pd.Skip) return;
                if (pd.Upload) {
                    if (!pd.Exists) {
                        say("Can't upload, file missing: " + pd.Path);
                        continue;
                    }
                    //TODO - do upload here

                } 

            }
            
        }

        public void CleanAll(){
            d.Run("/Clean Debug");
            d.Run("/Clean Release");
            d.Run("/Clean Trial");
        }

        public void BuildAll() {
            d.Run("/Build Debug");
            d.Run("/Build Release");
            d.Run("/Build Trial");
        }


        public void RemoveUselessFiles() {
            var f = new Futile(Console.Out);
            q = new FsQuery(this.f.parentPath, new string[]{"/.git","^/Releases", "^/Tests/Builder"});


            //delete /Tests/binaries  (*.pdb, *.xml, *.dll)
            //delete all bin and obj folders under /Tests and /Plugins
            //delete /Core/obj folder
            //Deleate all bin,obj,imageacache,uploads, and results folders under /Samples
            f.DelFiles(q.files("^/Tests/binaries/*.(pdb|xml|dll|txt)$"));
            f.DelFolders(q.folders("^/(Tests|Plugins)/*/(bin|obj)$",
                       "^/Samples/*/(bin|obj|imagecache|uploads|results)$",
                       "^/Core/obj$"));



            //delete .xml and .pdb files for third-party libs
            f.DelFiles(q.files("^/dlls/*/(Aforge|LitS3|Ionic)*.(pdb|xml)$"));

            //delete Thumbs.db
            //delete */.DS_Store
            f.DelFiles(q.files("/Thumbs.db$",
                                "/.DS_Store$"));
            q = null;
            
        }


        public string[] standardExclusions = new string[]{
                "/.git","^/Releases","^/Legacy","^/Tests/Builder","/thumbs.db$","/.DS_Store$",".suo$",".user$"
            };

        public void PrepareForPackaging() {
            if (q == null) q = new FsQuery(this.f.parentPath, standardExclusions);
            //Don't copy the DotNetZip xml file.
            q.exclusions.Add(new Pattern("^/Plugins/Libs/DotNetZip*.xml$"));
            q.exclusions.Add(new Pattern("^/Tests/Libs/LibDevCassini"));

        }
        public void PackMin(PackageDescriptor desc) {
            // 'min' - /dlls/release/ImageResizer.* - /
            // /*.txt
            using (var p = new Package(desc.Path, this.f.parentPath)) {
                p.Add(q.files("^/dlls/release/ImageResizer.(dll|pdb|xml)$"), "/");
                p.Add(q.files("^/[^/]+.txt$"));
            }
        }

        public void PackFull(PackageDescriptor desc) {
            // 'full'
            using (var p = new Package(desc.Path, this.f.parentPath)) {
                p.Add(q.files("^/(core|plugins|samples|tests)/"));
                p.Add(q.files("^/dlls/(release|trial)"));
                p.Add(q.files("^/[^/]+.txt$"));
            }
        }

        public void PackCore(PackageDescriptor desc) {
            // 'core' - 
            // /dlls/release/ImageResizer.* -> /
            // /dlls/debug/ImageResizer.* -> /
            // /Core/
            // /Samples/Images
            // /Samples/Core/
            // /*.txt
            using (var p = new Package(desc.Path, this.f.parentPath)) {
                p.Add(q.files("^/dlls/release/ImageResizer.(dll|pdb|xml)$"), "/");
                p.Add(q.files("^/Core/",
                              "^/Samples/Images/",
                              "^/Samples/BasicWebApplication/"));
                p.Add(q.files("^/[^/]+.txt$"));
            }
        }



        public void PackStandard(PackageDescriptor desc) {
            // 'standard'
            
            using (var p = new Package(desc.Path, this.f.parentPath)) {
                p.Add(q.files("^/dlls/release/ImageResizer.(dll|pdb|xml)$"), "/");
                p.Add(q.files("^/dlls/trial/"), "/TrialPlugins/");
                p.Add(q.files("^/(core|samples)/"));
                p.Add(q.files("^/[^/]+.txt$"));
            }
        }



    }
}
