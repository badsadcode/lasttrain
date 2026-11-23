using System;
using Godot;
using LastTrain.Simulation;

namespace LastTrain.Audio;

/// <summary>
/// Placeholder procedural audio, synthesised from noise at 22 kHz (lo-fi on purpose):
///  - Exhaust chuffs: 4 per driving-wheel revolution, louder when working harder.
///  - Rail-joint clicks every 39 ft rail length.
///  - Fire roar in the cab, safety valve roar, injector hiss.
/// Real recordings replace this later; the timing logic can stay.
/// </summary>
public partial class LocomotiveAudio : Node
{
    const float MixRate = 22050f;
    const double RailLengthM = 11.9;           // 39 ft rails
    static readonly double[] AxleOffsetsM = { 0, 2.1, 4.2, 9.0, 11.0 }; // rough wheel positions behind the cab

    LocomotiveSim _sim = null!;
    AudioStreamGeneratorPlayback? _playback;
    readonly Random _rng = new(1934);

    double _wheelPhase;
    float _chuffEnv, _chuffLevel;
    float _lowNoise, _bandNoise, _roar;
    float _clickEnv;
    readonly long[] _lastJoint = new long[AxleOffsetsM.Length];

    public LocomotiveAudio Init(LocomotiveSim sim)
    {
        _sim = sim;
        Name = "LocomotiveAudio";
        return this;
    }

    public override void _Ready()
    {
        var player = new AudioStreamPlayer
        {
            Stream = new AudioStreamGenerator { MixRate = MixRate, BufferLength = 0.15f },
            VolumeDb = -4f,
        };
        AddChild(player);
        player.Play();
        _playback = player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
        for (int i = 0; i < _lastJoint.Length; i++) _lastJoint[i] = JointIndex(i);
    }

    long JointIndex(int axle) => (long)Math.Floor((_sim.DistanceM - AxleOffsetsM[axle]) / RailLengthM);

    public override void _Process(double delta)
    {
        if (_playback == null) return;

        // Rail joints: trigger a click when any axle crosses a joint since last frame.
        for (int i = 0; i < _lastJoint.Length; i++)
        {
            long j = JointIndex(i);
            if (j != _lastJoint[i])
            {
                _lastJoint[i] = j;
                _clickEnv = Math.Max(_clickEnv, (float)Math.Clamp(Math.Abs(_sim.SpeedMs) / 15.0, 0.15, 0.7));
            }
        }

        double v = Math.Abs(_sim.SpeedMs);
        double chuffsPerSecond = v / (Math.PI * _sim.Spec.DriverDiameterM) * 4.0;
        if (_sim.WheelSlip) chuffsPerSecond *= 3.0;
        double phaseStep = chuffsPerSecond / MixRate;
        // Chuffs blur together at speed: shorter decay.
        float decaySeconds = (float)Math.Clamp(0.5 / Math.Max(chuffsPerSecond, 0.1), 0.03, 0.16);
        float chuffDecay = Mathf.Exp(-1f / (decaySeconds * MixRate));
        float work = (float)Math.Clamp(_sim.SteamDemand * 2.0, 0, 1);

        float fireLevel = (float)_sim.Boiler.FireBed * 0.06f;
        float valve = _sim.Boiler.SafetyValveLifting ? 0.35f : 0f;
        float injector = _sim.Boiler.InjectorWorking ? 0.05f : 0f;

        int frames = _playback.GetFramesAvailable();
        for (int i = 0; i < frames; i++)
        {
            _wheelPhase += phaseStep;
            if (_wheelPhase >= 1.0)
            {
                _wheelPhase -= 1.0;
                _chuffEnv = 1f;
                // Coasting with the regulator shut still makes a faint "drift" puff from the cylinders.
                _chuffLevel = 0.04f + 0.9f * work;
            }

            float noise = (float)(_rng.NextDouble() * 2 - 1);
            _lowNoise += (noise - _lowNoise) * 0.18f;       // dark, chesty noise for chuffs
            _bandNoise += (noise - _bandNoise) * 0.6f;      // brighter
            float hiss = noise - _bandNoise;                // high-passed: steam hiss
            _roar += (noise - _roar) * 0.02f;               // very low rumble: the fire

            float s = _lowNoise * _chuffEnv * _chuffLevel * 1.6f
                    + _roar * fireLevel * 6f
                    + hiss * (valve + injector)
                    + noise * _clickEnv * 0.5f;

            _chuffEnv *= chuffDecay;
            _clickEnv *= 0.993f;

            s = Math.Clamp(s, -1f, 1f);
            _playback.PushFrame(new Vector2(s, s));
        }
    }
}
