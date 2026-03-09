using System.Reflection;
using InventorySystem;
using InventorySystem.Items.Autosync;
using NetworkManagerUtils.Dummies;
using UnityEngine;

namespace Causality0.Core;

public sealed class DummyInputWrapper : MonoBehaviour
{
    private static readonly FieldInfo _f = typeof(AutosyncItem).GetField("_dummyEmulator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly MethodInfo _am = typeof(DummyKeyEmulator).GetMethod("AddEntry", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(ActionName), typeof(bool) }, null);

    private static readonly MethodInfo _rm = typeof(DummyKeyEmulator).GetMethod("RemoveEntry", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(ActionName) }, null);

    public DummyKeyEmulator Emulator { get; private set; }

    public void Bind(Inventory inv)
    {
        if (Emulator == null && inv != null)
        {
            Emulator = new DummyKeyEmulator(inv);
        }
    }

    public void InjectInto(AutosyncItem item)
    {
        if (item == null || Emulator == null || _f == null)
        {
            return;
        }

        try
        {
            _f.SetValue(item, Emulator);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Causality] Reflection inject failed: " + ex);
        }
    }

    public void Add(ActionName a, bool click)
    {
        if (Emulator == null || _am == null)
        {
            return;
        }

        try
        {
            _am.Invoke(Emulator, new object[] { a, click });
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Causality] Reflection add failed: " + ex);
        }
    }

    public void Remove(ActionName a)
    {
        if (Emulator == null || _rm == null)
        {
            return;
        }

        try
        {
            _rm.Invoke(Emulator, new object[] { a });
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Causality] Reflection remove failed: " + ex);
        }
    }
}
