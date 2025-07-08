using Avalonia.Controls.Documents;
using LiteSkinViewer3D.Shared.Enums;
using LiteSkinViewer3D.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LiteSkinViewer3D.Shared;

/// <summary>
/// 角色动画的当前状态，支持左右身体部件的独立控制
/// </summary>
public sealed class SkinAnimationState {
    public Vector3 Body = Vector3.Zero;
    public Vector3 ArmLeft = Vector3.Zero;
    public Vector3 ArmRight = Vector3.Zero;
    public Vector3 LegLeft = Vector3.Zero;
    public Vector3 LegRight = Vector3.Zero;
    public Vector3 Head = Vector3.Zero;

    public float Cape = 0f;
    public float Time = 0f;
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
    private readonly Random _rng = new();

    public bool IsEnable { get; set; }
    public SkinType SkinType { get; set; }
    public ISkinAnimation Controller { get; set; }
    public SkinAnimationState State { get; } = new();
    public double IdleIntervalSeconds { get; set; } = 10.0;

    public SkinAnimationController(ISkinAnimation controller = default!) {
        Controller = controller ?? new LookAroundAnimation();
        _activeIdle = Controller;
    }

    public void Close() {
        _closed = true;
        IsEnable = false;
    }

    public bool Tick(double deltaTime) {
        if (!IsEnable) return !_closed;

        _tickAccum += deltaTime;
        _idleTimer += deltaTime;

        // 帧控制
        while (_tickAccum > 0.01) {
            _tickAccum -= 0.01;
            _frame = (_frame + 1) % 120;
        }

        // 每过指定秒数，强制执行 Idle 动画
        if (_idleTimer >= IdleIntervalSeconds &&
            Controller.EnableIdle &&
            Controller.IdleAnimations.Count > 0) {
            _idleTimer = 0;

            _activeIdle = Controller.IdleAnimations.Count switch {
                1 => Controller.IdleAnimations[0],
                > 1 => Controller.IdleAnimations[_rng.Next(Controller.IdleAnimations.Count)],
                _ => Controller
            };

            _activeIdle.OnIdleStart(State);
        }

        _activeIdle.Tick(State, _frame, deltaTime, SkinType);
        return !_closed;
    }
}

public sealed class DefaultAnimation : ISkinAnimation {
    public bool EnableIdle => true;
    public IReadOnlyList<ISkinAnimation> IdleAnimations => [
        new LookAroundAnimation()
    ];

    public void OnIdleStart(SkinAnimationState state) {
        state.Time = 0f; // 初始化时间流
    }

    public void Tick(SkinAnimationState state, int frame, double deltaTime, SkinType type) {
        state.Time += (float)deltaTime * 1f; // 节奏调慢

        float t = state.Time * MathF.PI * 2;
        float tSlow = state.Time * MathF.PI;

        float pulse = Ease((MathF.Sin(t * 0.5f) + 1f) / 2f);

        // 💪 手臂运动：左右甩 + 向后张 + 前后轻摇
        float armZ = MathF.Sin(t * 0.9f) * 7.0f;
        float armX = pulse * 5.0f;
        float armY = MathF.Sin(t * 0.4f) * 13.0f;

        state.ArmLeft.Z = armZ;
        state.ArmRight.Z = -armZ;

        state.ArmLeft.X = armX;
        state.ArmRight.X = armX;

        state.ArmLeft.Y = armY;
        state.ArmRight.Y = -armY;

        state.Head.Y = MathF.Sin(tSlow * 0.8f + 0.5f) * 5.0f;
        state.Cape = 0.25f + MathF.Sin(tSlow * 0.85f + 0.5f) * 0.5f;

        static float Ease(float x) => -(MathF.Cos(MathF.PI * x) - 1f) / 2f;
    }
}

public sealed class LookAroundAnimation : ISkinAnimation {
    public bool EnableIdle => true;
    public IReadOnlyList<ISkinAnimation> IdleAnimations => [];

    private float _time = 0f;
    private float _yaw = 0f;
    private float _targetYaw = 0f;
    private float _switchTimer = 0f;
    private float _switchInterval = 1.5f;
    private float _lookShockTimer = 0f;
    private readonly Random _rng = new();

    public void OnIdleStart(SkinAnimationState state) {
        _time = 0f;
        _yaw = state.Head.Z;
        _targetYaw = GetRandomYaw();
        _switchTimer = 0f;
        _lookShockTimer = 0f;
    }

    public void Tick(SkinAnimationState state, int frame, double deltaTime, SkinType type) {
        _time += (float)deltaTime;
        _switchTimer += (float)deltaTime;
        _lookShockTimer += (float)deltaTime;

        if (_switchTimer >= _switchInterval) {
            _switchTimer = 0f;
            _targetYaw = GetRandomYaw();
            _switchInterval = 1.2f + (float)_rng.NextDouble() * 1.0f;
        }

        if (_lookShockTimer >= 7.5f) {
            _lookShockTimer = 0f;
            _targetYaw = _rng.Next(0, 2) == 0 ? -100f : 100f;
        }

        _yaw = SmoothApproach(_yaw, _targetYaw, (float)deltaTime, 0.25f);
        float jitter = MathF.Sin(_time * 13.3f + 3.1f) * 0.5f;

        float t = _time * MathF.PI;
        float armZ = MathF.Sin(t * 0.9f) * 7.0f;
        float armY = MathF.Sin(t * 0.4f) * 13.0f;

        state.Head.Z = _yaw + jitter;
        state.Head.Y = MathF.Sin(_time * 1.7f + 0.2f) * 5.5f;

        state.ArmLeft.Z = armZ;
        state.ArmRight.Z = armZ;
        state.ArmLeft.Y = armY;
        state.ArmRight.Y = armY;

        state.Body.Z = MathF.Sin(_time * 1.6f) * 3.2f - 2.5f;
        state.Cape = 0.25f + MathF.Sin(_time * 1.2f + 0.4f) * 0.25f;
    }

    private float GetRandomYaw() {
        return _rng.NextSingle() switch {
            < 0.3f => -50f,
            < 0.6f => 0f,
            _ => 50f
        };
    }

    private static float SmoothApproach(float current, float target, float deltaTime, float smoothTime) {
        float t = 1f - MathF.Exp(-deltaTime * 10f / smoothTime);
        return current + (target - current) * t;
    }
}