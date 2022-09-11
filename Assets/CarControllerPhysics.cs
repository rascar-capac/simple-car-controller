using System;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CarControllerPhysics : MonoBehaviour
{
#region properties
    public float Speed => _rb.velocity.magnitude * 3.6f;
    public MotorState MotorState => _motorState;
    public bool HandbrakeIsSet => _handbrakeIsSet;
    public float AccelerationInput => _accelerationInput;
#endregion



#region fields
    [SerializeField] private List<AxleInfo> _axleInfos;
    [SerializeField] private float _fullMotorTorque;
    [SerializeField] [Tooltip("Maximum speed in km/h")] private float _maxSpeed;
    [SerializeField] [Tooltip("Maximum reverse speed in km/h")] private float _maxReverseSpeed;
    [SerializeField] private float _fullSteeringAngle;
    [SerializeField] [Tooltip("Intensity of the brakes in comparison to the full motor torque")] private float _brakesFactor;
    [SerializeField] [Tooltip("Intensity of the handbrake in comparison to the full motor torque")] private float _handbrakeFactor;
    [SerializeField] [Tooltip("Intensity of the engine brake in comparison to the full motor torque")] private float _engineBrakeFactor;
    [SerializeField] private MeshRenderer _rearLight;
    [SerializeField] private Color _brakeLightColor;
    [SerializeField] private Color _reverseLightColor;
    [SerializeField] private CinemachineVirtualCamera _vcam;
    [SerializeField] private float _noiseFactor;
    [SerializeField] private float _initialFOV;
    [SerializeField] private float _maxSpeedFOV;
    private Rigidbody _rb;
    private MotorState _motorState;
    private float _accelerationInput;
    private float _steeringInput;
    private bool _handbrakeIsSet;
#endregion



#region unity messages
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _axleInfos[0].LeftWheel.ConfigureVehicleSubsteps(5, 12, 15); // to avoid jitter
    }

    private void FixedUpdate()
    {
        UpdateMotorState();
        UpdateMovement();
    }

    private void Update()
    {
        UpdateInput();
        UpdateCamera();
        UpdateRearLight();
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(0, 0, 150, 100), "");
        GUI.Label(new Rect(5, 5, 145, 20), _accelerationInput.ToString());
        GUI.Label(new Rect(5, 20, 145, 20), _motorState.ToString());
        GUI.Label(new Rect(5, 35, 145, 20), Speed.ToString());
    }
#endregion



#region private methods
    private void UpdateInput()
    {
        _accelerationInput = Input.GetAxis("Vertical");
        _steeringInput = Input.GetAxis("Horizontal");
        _handbrakeIsSet = Input.GetButton("Fire3");
    }

    private void UpdateMotorState()
    {
        if (_accelerationInput > 0)
        {
            _motorState = MotorState.ACCELERATING;
        }
        else if (_accelerationInput < 0)
        {
            if (_rb.velocity.magnitude < 0.01f ||
                    Vector3.Dot(_rb.velocity, -transform.forward) > 0)
            {
                _motorState = MotorState.REVERSING;
            }
            else
            {
                _motorState = MotorState.BRAKING;
            }
        }
        else
        {
            _motorState = MotorState.IDLE;
        }
    }

    private void UpdateMovement()
    {
        float motorTorque = _fullMotorTorque * _accelerationInput;
        float steeringAngle = _fullSteeringAngle * _steeringInput;

        foreach (var axleInfo in _axleInfos)
        {
            axleInfo.LeftWheel.motorTorque = 0f;
            axleInfo.RightWheel.motorTorque = 0f;
            axleInfo.LeftWheel.brakeTorque = 0f;
            axleInfo.RightWheel.brakeTorque = 0f;
            if (axleInfo.HasSteering)
            {
                axleInfo.LeftWheel.steerAngle = steeringAngle;
                axleInfo.RightWheel.steerAngle = steeringAngle;
            }
            switch (_motorState)
            {
                case MotorState.BRAKING:
                    axleInfo.LeftWheel.brakeTorque = -motorTorque * _brakesFactor;
                    axleInfo.RightWheel.brakeTorque = -motorTorque * _brakesFactor;
                    break;
                case MotorState.ACCELERATING:
                    if (axleInfo.HasMotor && Speed < _maxSpeed)
                    {
                        axleInfo.LeftWheel.motorTorque = motorTorque;
                        axleInfo.RightWheel.motorTorque = motorTorque;
                    }
                    break;
                case MotorState.REVERSING:
                    if (axleInfo.HasMotor && Speed < _maxReverseSpeed)
                    {
                        axleInfo.LeftWheel.motorTorque = motorTorque;
                        axleInfo.RightWheel.motorTorque = motorTorque;
                    }
                    break;
                case MotorState.IDLE:
                    axleInfo.LeftWheel.brakeTorque = _fullMotorTorque * _engineBrakeFactor;
                    axleInfo.RightWheel.brakeTorque = _fullMotorTorque * _engineBrakeFactor;
                    break;
            }
            UpdateWheelVisuals(axleInfo.LeftWheel);
            UpdateWheelVisuals(axleInfo.RightWheel);

            if (_handbrakeIsSet && !axleInfo.HasMotor)
            {
                axleInfo.LeftWheel.brakeTorque = _fullMotorTorque * _handbrakeFactor;
                axleInfo.RightWheel.brakeTorque = _fullMotorTorque * _handbrakeFactor;
            }
        }
    }

    private void UpdateWheelVisuals(WheelCollider collider)
    {
        if (collider.transform.childCount == 0) return;

        Transform visualWheel = collider.transform.GetChild(0);
        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        visualWheel.transform.position = position;
        visualWheel.transform.rotation = rotation;
    }

    private void UpdateRearLight()
    {
        switch (_motorState)
        {
            case MotorState.REVERSING:
                _rearLight.gameObject.SetActive(true);
                _rearLight.material.SetColor("_EmissionColor", _reverseLightColor * 4f);
                break;
            case MotorState.BRAKING:
                _rearLight.gameObject.SetActive(true);
                _rearLight.material.SetColor("_EmissionColor", _brakeLightColor * 4f);
                break;
            default:
                _rearLight.gameObject.SetActive(false);
                break;
        }
    }

    private void UpdateCamera()
    {
        CinemachineBasicMultiChannelPerlin noise = _vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        if (noise)
        {
            noise.m_AmplitudeGain = Mathf.Pow(Speed / _maxSpeed, 2) * _noiseFactor;
        }
        _vcam.m_Lens.FieldOfView = Remap(Speed/ _maxSpeed, 0f, 1f, _initialFOV, _maxSpeedFOV);
    }

    public static float Remap(float value, float min1, float max1, float min2, float max2)
    {
        return min2 + (value - min1) * ((max2 - min2) / (max1 - min1));
    }
#endregion
}

[System.Serializable]
public class AxleInfo
{

    public WheelCollider LeftWheel => _leftWheel;
    public WheelCollider RightWheel => _rightWheel;
    public bool HasMotor => _hasMotor;
    public bool HasSteering => _hasSteering;

    [SerializeField] private WheelCollider _leftWheel;
    [SerializeField] private WheelCollider _rightWheel;
    [SerializeField] private bool _hasMotor;
    [SerializeField] private bool _hasSteering;
}

[Flags]
public enum MotorState
{
    ACCELERATING =1,
    BRAKING = 2,
    REVERSING = 4,
    IDLE = 8
}