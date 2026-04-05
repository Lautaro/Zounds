// ZoundsDependencyCheck.cs
// Checks for required packages on domain reload and prompts the user to install them.

using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
static class ZoundsDependencyCheck
{
    static ListRequest _listRequest;
    static AddRequest _addRequest;

    static ZoundsDependencyCheck()
    {
#if !ADDRESSABLES_INSTALLED
        // Check if Addressables is truly missing (not just a stale define)
        _listRequest = Client.List(true);
        EditorApplication.update += CheckPackageList;
#endif
    }

    static void CheckPackageList()
    {
        if (_listRequest == null || !_listRequest.IsCompleted) return;
        EditorApplication.update -= CheckPackageList;

        if (_listRequest.Status == StatusCode.Failure)
        {
            Debug.LogWarning("[Zounds] Could not check installed packages: " + _listRequest.Error?.message);
            return;
        }

        bool hasAddressables = false;
        foreach (var pkg in _listRequest.Result)
        {
            if (pkg.name == "com.unity.addressables")
            {
                hasAddressables = true;
                break;
            }
        }

        if (!hasAddressables)
        {
            bool install = EditorUtility.DisplayDialog(
                "Zounds — Missing Dependency",
                "Zounds requires the Addressables package (com.unity.addressables).\n\n" +
                "Install it now?",
                "Install Addressables", "Not Now");

            if (install)
            {
                _addRequest = Client.Add("com.unity.addressables");
                EditorApplication.update += CheckInstallProgress;
                Debug.Log("[Zounds] Installing com.unity.addressables...");
            }
            else
            {
                Debug.LogWarning("[Zounds] Addressables not installed. Some Zounds features will not work.");
            }
        }

        _listRequest = null;
    }

    static void CheckInstallProgress()
    {
        if (_addRequest == null || !_addRequest.IsCompleted) return;
        EditorApplication.update -= CheckInstallProgress;

        if (_addRequest.Status == StatusCode.Success)
            Debug.Log($"[Zounds] Installed {_addRequest.Result.displayName} {_addRequest.Result.version}");
        else
            Debug.LogError("[Zounds] Failed to install Addressables: " + _addRequest.Error?.message);

        _addRequest = null;
    }
}
