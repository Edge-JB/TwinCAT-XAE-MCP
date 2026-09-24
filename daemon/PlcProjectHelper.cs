using System;
using TCatSysManagerLib;

namespace Te1000Daemon
{
    public sealed class PlcProjectIdentity
    {
        public string PlcPath;
        public string PlcName;
        public string ProjectPath;
        public string ProjectName;
        public object PlcRoot;
        public object NestedProject;
    }

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
        public static PlcProjectIdentity Resolve(dynamic systemManager, string plcPath)
        {
            if (string.IsNullOrWhiteSpace(plcPath))
            {
                dynamic tipc = ComHelpers.GetTreeItem(systemManager, "TIPC");
                if (ComHelpers.ChildCount(tipc) < 1)
                    throw new BridgeException("No PLC project found under TIPC");
                dynamic first = ComHelpers.Child(tipc, 1);
                string firstName = ComHelpers.SafeStr(delegate { return first.Name; });
                if (string.IsNullOrWhiteSpace(firstName))
                    throw new BridgeException("The first PLC project under TIPC has no usable name");
                plcPath = "TIPC^" + firstName;
            }

            PathUtil.AssertNotSafetyPath(plcPath);
            dynamic root = ComHelpers.GetTreeItem(systemManager, plcPath);
            string plcName = ComHelpers.SafeStr(delegate { return root.Name; });
            if (string.IsNullOrWhiteSpace(plcName))
                throw new BridgeException("PLC root '" + plcPath + "' has no usable name");
            object nested;
            try
            {
                nested = ((ITcProjectRoot)(object)root).NestedProject;
            }
            catch (Exception ex)
            {
                throw new BridgeException("PLC root '" + plcPath +
                    "' does not implement ITcProjectRoot or expose NestedProject: " + ex.Message);
            }
            if (nested == null)
                throw new BridgeException("PLC root '" + plcPath + "' exposes no nested IEC project");

            ITcSmTreeItem nestedItem;
            try
            {
                nestedItem = (ITcSmTreeItem)nested;
            }
            catch (Exception ex)
            {
                throw new BridgeException("NestedProject of PLC root '" + plcPath +
                    "' does not implement ITcSmTreeItem: " + ex.Message);
            }

            string projectName = nestedItem.Name;
            if (string.IsNullOrWhiteSpace(projectName))
                throw new BridgeException("NestedProject of PLC root '" + plcPath + "' has no usable name");

            string projectPath = null;
            try { projectPath = nestedItem.PathName; }
            catch { projectPath = null; }
            if (string.IsNullOrWhiteSpace(projectPath))
                projectPath = plcPath + "^" + projectName;

            return new PlcProjectIdentity
            {
                PlcPath = plcPath,
                PlcName = plcName,
                ProjectPath = projectPath,
                ProjectName = projectName,
                PlcRoot = (object)root,
                NestedProject = nested
            };
        }

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
        // nested project INSTANCE node.
        public static bool CheckAll(object iecProject)
        {
            return ((ITcPlcIECProject2)iecProject).CheckAllObjects();
        }

        // ITcProjectRoot.NestedProject is the documented identity read on the PLC root.
        public static string GetNestedProjectName(object projectRoot)
        {
            try
            {
                ITcProjectRoot typed = (ITcProjectRoot)projectRoot;
                object nested = typed.NestedProject;
                if (nested == null) { return null; }
                return ((ITcSmTreeItem)nested).Name;
            }
            catch { return null; }
        }

        // First ordinary child of the PLC root is the runtime project instance.
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
