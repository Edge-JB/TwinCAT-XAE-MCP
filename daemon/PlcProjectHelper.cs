using System;
using TCatSysManagerLib;

namespace Te1000Daemon
{
    // Verbatim port of the bridge's compiled Te1000PlcProjectHelper (L782-851).
    //
    // ITcPlcProject / ITcPlcIECProject(2) / ITcProjectRoot / ITcSmTreeItem /
    // ITcPlcTaskReference are vtable (IUnknown) interfaces. Late-bound `dynamic`
    // (IDispatch) cannot QI to them, so these typed casts live here. The
    // TCatSysManagerLib reference is EmbedInteropTypes=true so no extra DLL ships.
    //
    // QI/marshaling also requires the interface registered in the 64-bit registry
    // view (it is, on this machine — same precondition as the PS helper).
    public static class PlcProjectHelper
    {
        public static bool GetAutostart(object plcProject)
        {
            return ((ITcPlcProject)plcProject).BootProjectAutostart;
        }

        public static void Deploy(object plcProject, bool autostart, bool activate)
        {
            ITcPlcProject typed = (ITcPlcProject)plcProject;
            typed.BootProjectAutostart = autostart;
            typed.GenerateBootProject(activate);
        }

        // ITcPlcProject lives on the PLC ROOT node (TIPC^<name>); set config-only flags.
        public static object[] SetBootFlags(object plcProject, bool hasAutostart, bool autostart, bool hasTmc, bool tmc)
        {
            ITcPlcProject typed = (ITcPlcProject)plcProject;
            if (hasAutostart) { typed.BootProjectAutostart = autostart; }
            if (hasTmc) { typed.TmcFileCopy = tmc; }
            return new object[] { typed.BootProjectAutostart, typed.TmcFileCopy };
        }

        // CheckAllObjects (build-validate) lives on ITcPlcIECProject2 on the
        // nested IEC project node (ITcProjectRoot.NestedProject of the PLC root).
        public static bool CheckAll(object iecProject)
        {
            return ((ITcPlcIECProject2)iecProject).CheckAllObjects();
        }

        // The nested IEC project of a PLC root, resolved through the
        // Automation Interface rather than by display name.
        //
        // The nested project's tree name is localized by the XAE shell
        // ("<name> Project" on English installs, "<name> Projekt" on German
        // ones, ...) and follows the user-chosen project name, so callers must
        // never construct it from the PLC root name + " Project" (issue #11).
        // ITcProjectRoot.NestedProject is language-independent and hands back
        // the exact object; ITcSmTreeItem.PathName is its exact tree path.
        public sealed class NestedProject
        {
            public object Item;   // the ITcSmTreeItem / ITcPlcIECProject(2) object
            public string Name;   // e.g. "Example Projekt"
            public string Path;   // e.g. "TIPC^Example^Example Projekt"
        }

        // Returns null when the root does not expose ITcProjectRoot, has no
        // nested project, or the read fails — callers fall back to probing.
        public static NestedProject ResolveNestedProject(object projectRoot)
        {
            try
            {
                ITcProjectRoot typed = (ITcProjectRoot)projectRoot;
                object nested = typed.NestedProject;
                if (nested == null) { return null; }
                ITcSmTreeItem item = (ITcSmTreeItem)nested;
                NestedProject r = new NestedProject();
                r.Item = nested;
                r.Name = item.Name;
                try { r.Path = item.PathName; } catch { r.Path = null; }
                return r;
            }
            catch { return null; }
        }

        // ITcProjectRoot.NestedProject is the documented identity read on the PLC root.
        public static string GetNestedProjectName(object projectRoot)
        {
            NestedProject nested = ResolveNestedProject(projectRoot);
            return nested == null ? null : nested.Name;
        }

        // First child of the PLC root is the project instance node ('<name> Instance').
        // The nested IEC project is NOT enumerated as a child; use ResolveNestedProject.
        public static string GetInstanceName(object treeItem)
        {
            try
            {
                ITcSmTreeItem typed = (ITcSmTreeItem)treeItem;
                if (typed.ChildCount < 1) { return null; }
                ITcSmTreeItem child = typed.get_Child(1);
                return child == null ? null : child.Name;
            }
            catch { return null; }
        }

        // ITcPlcTaskReference lives on the PlcTask node under the project instance.
        public static string SetLinkedTask(object taskRef, string taskPath)
        {
            ITcPlcTaskReference typed = (ITcPlcTaskReference)taskRef;
            typed.LinkedTask = taskPath;
            return typed.LinkedTask;
        }

        // Typed read of ITcPlcTaskReference.LinkedTask (vtable; dynamic cannot QI).
        // Mirrors the bridge's Te1000PlcTaskRefHelper::GetLinkedTask. Used by
        // tc_task get_linked_task and as the feature-detect probe in set_linked_task.
        public static string GetLinkedTask(object taskRef)
        {
            return ((ITcPlcTaskReference)taskRef).LinkedTask;
        }

        // ITcPlcIECProject on the project INSTANCE node: PLCopen + library.
        public static void PlcOpenExport(object iecProject, string file, string selection)
        {
            ((ITcPlcIECProject)iecProject).PlcOpenExport(file, selection);
        }

        public static void PlcOpenImport(object iecProject, string file, int options, string selection, bool folderStructure)
        {
            ((ITcPlcIECProject)iecProject).PlcOpenImport(file, options, selection, folderStructure);
        }

        public static void SaveAsLibrary(object iecProject, string file, bool install)
        {
            ((ITcPlcIECProject)iecProject).SaveAsLibrary(file, install);
        }
    }
}
