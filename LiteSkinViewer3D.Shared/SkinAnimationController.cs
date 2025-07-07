using Avalonia.Controls.Documents;
using LiteSkinViewer3D.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LiteSkinViewer3D.Shared;

/// <summary>
/// 角色动画的当前状态，支持左右身体部件的独立控制
/// </summary>
public sealed class SkinAnimationState {
    public Vector3 ArmLeft = Vector3.Zero;
    public Vector3 ArmRight = Vector3.Zero;
    public Vector3 LegLeft = Vector3.Zero;
    public Vector3 LegRight = Vector3.Zero;
    public Vector3 Head = Vector3.Zero;

    public float Cape = 0f;
}

/// <summary>
/// 皮肤动画统一接口
/// </summary>
public interface ISkinAnimation {
    /// <summary>
    /// 驱动每帧动画
    /// </summary>
    void Tick(SkinAnimationState state, int frame, double deltaTime, SkinType skinType);

    /// <summary>
    /// 是否启用待机系统
    /// </summary>
    bool EnableIdle { get; }

    /// <summary>
    /// 待机动画集合（可为空或只含一个）
    /// </summary>
    IReadOnlyList<ISkinAnimation> IdleAnimations { get; }

    /// <summary>
    /// 在动画进入 Idle 状态时触发
    /// </summary>
    void OnIdleStart(SkinAnimationState state);
}

/// <summary>
/// 皮肤动画控制器
/// </summary>
public class SkinAnimationController {
    private int _frame = 0;
    private double _tickAccum = 0;
    private double _idleTimer = 0;
    private bool _closed = false;

    private ISkinAnimation _activeIdle;
    private bool _enteredIdle = false;
    private readonly Random _rng = new();

    public bool IsEnable { get; set; }
    public SkinType SkinType { get; set; }
    public SkinAnimationState State { get; } = new();

    /// <summary>
    /// 当前动画控制器
    /// </summary>
    public ISkinAnimation Controller { get; set; }

    public SkinAnimationController(ISkinAnimation controller = default!) {
        Controller = _activeIdle = controller ?? new DefaultAnimation();
    }

    public void Close() {
        _closed = true;
        IsEnable = false;
    }

    public bool Tick(double deltaTime) {
        if (IsEnable) {
            _tickAccum += deltaTime;
            _idleTimer += deltaTime;

            while (_tickAccum > 0.01) {
                _tickAccum -= 0.01;
                _frame = (_frame + 1) % 120;
            }

            if (Controller.EnableIdle && _idleTimer >= 10.0 && Controller.IdleAnimations.Count > 0) {
                if (!_enteredIdle) {
                    _enteredIdle = true;

                    _activeIdle = Controller.IdleAnimations.Count switch {
                        1 => Controller.IdleAnimations[0],
                        > 1 => Controller.IdleAnimations[_rng.Next(Controller.IdleAnimations.Count)],
                        _ => Controller
                    };

                    _activeIdle.OnIdleStart(State);
                }

                _activeIdle.Tick(State, _frame, deltaTime, SkinType);
            } else {
                _enteredIdle = false;
                Controller.Tick(State, _frame, deltaTime, SkinType);
                _idleTimer = 0;
            }
        }

        return !_closed;
    }
}

public class DefaultAnimation : ISkinAnimation {
    public bool EnableIdle => false;
    public IReadOnlyList<ISkinAnimation> IdleAnimations => [];

    public void OnIdleStart(SkinAnimationState state) {
        state.ArmLeft = Vector3.Zero;
        state.ArmRight = Vector3.Zero;
        state.LegLeft = Vector3.Zero;
        state.LegRight = Vector3.Zero;
        state.Head = Vector3.Zero;
        state.Cape = 0f;
    }

    public void Tick(SkinAnimationState state, int frame, double deltaTime, SkinType type) {
        float t = frame / 120f * MathF.PI * 2; // 每 120 帧一个呼吸周期 ≈ 2秒

        // 💨 手臂随“扩胸”略张（Z轴），模拟吸气动作
        float armZ = MathF.Sin(t) * 2.0f;  // 左右对称张开
        state.ArmLeft.Z = armZ;
        state.ArmRight.Z = -armZ;

        // 🧠 头部轻点头 + 微晃（X 前后，Y 左右）
        state.Head.X = MathF.Sin(t * 0.5f) * 2.0f;
        state.Head.Y = MathF.Cos(t * 0.25f) * 1.5f;

        // 🎽 披风轻微抖动模拟节奏
        state.Cape = 0.2f + MathF.Sin(t * 0.8f + 0.5f) * 0.15f;
    }
}
