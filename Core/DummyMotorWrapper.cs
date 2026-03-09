using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using RelativePositioning;
using UnityEngine;

namespace Causality0.Core;

public sealed class DummyMotorWrapper : MonoBehaviour
{
    private FpcMotor _m;
    private IFpcRole _r;
    private Vector3 _v;
    private bool _g;
    private bool _ok;
    private PlayerMovementState _s;

    public float MoveSpeed = 30f;

    public void Bind(ReferenceHub h)
    {
        _r = h?.roleManager?.CurrentRole as IFpcRole;
        _m = _r?.FpcModule?.Motor;
    }

    public void SetFakeVelocity(Vector3 v, bool g, PlayerMovementState s)
    {
        _v = v;
        _g = g;
        _s = s;
        _ok = true;
    }

    private void LateUpdate()
    {
        if (!_ok)
        {
            return;
        }

        if (_m == null || _r?.FpcModule == null)
        {
            Bind(GetComponent<ReferenceHub>());
            if (_m == null || _r?.FpcModule == null)
            {
                return;
            }
        }

        _r.FpcModule.CurrentMovementState = _s;
        _r.FpcModule.IsGrounded = _g;
        Vector3 p = _r.FpcModule.Position;
        Vector3 d = _v;
        d.y = 0f;
        if (d.sqrMagnitude <= 0.0001f)
        {
            _m.ReceivedPosition = new RelativePosition(p);
            return;
        }

        d.Normalize();
        _m.ReceivedPosition = new RelativePosition(p + d * (MoveSpeed * Time.deltaTime));
    }
}