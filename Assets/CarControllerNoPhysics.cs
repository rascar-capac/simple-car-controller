using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CarControllerNoPhysics : MonoBehaviour
{
#region private fields
    [SerializeField] [Tooltip("Max speed in km/h")] private float _maxSpeed;
    [SerializeField] [Tooltip("Max reverse speed in km/h")] private float _maxReverseSpeed;
    [SerializeField] [Tooltip("Required time to accelerate from 0 to 100 km/h")] private float _accelerationTime;
    [SerializeField] [Tooltip("Required time to decelerate from 100 to 0 km/h")] private float _decelerationTime;
    [SerializeField] [Tooltip("Required time to brake from 100 to 0 km/h")] private float _brakeTime;
    [SerializeField] private float _maxSteeringAngle;
    [SerializeField] private MeshRenderer _rearLight;
    [SerializeField] private Color _brakeLightColor;
    [SerializeField] private Color _reverseLightColor;
    [SerializeField] private List<Transform> _steeringWheels;
    [SerializeField] private CinemachineVirtualCamera _vcam;
    private float _accelerationInput;
    private float _steeringInput;
    private bool _handbrakeIsSet;
    private MotorState _motorState;
    private float _currentSpeed;
    private float _smoothDampCurrentState;
#endregion



#region unity messages
    private void Update()
    {
        UpdateInput();
        UpdateMotorState();
        UpdateAcceleration();
        UpdateSteering();
        UpdateRearLight();
        UpdateCamera();
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(0, 0, 150, 100), "");
        GUI.Label(new Rect(5, 5, 145, 20), _accelerationInput.ToString());
        GUI.Label(new Rect(5, 20, 145, 20), _motorState.ToString());
        GUI.Label(new Rect(5, 35, 145, 20), _currentSpeed.ToString());
    }
#endregion



#region private methods
    private void UpdateMotorState()
    {
        if (_accelerationInput > 0)
        {
            _motorState = MotorState.ACCELERATING;
        }
        else if (_accelerationInput < 0)
        {
            if (_currentSpeed <= 0)
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

    private void UpdateInput()
    {
        _accelerationInput = Input.GetAxis("Vertical");
        _steeringInput = Input.GetAxis("Horizontal");
        _handbrakeIsSet = Input.GetButton("Fire3");
    }

    private void UpdateAcceleration()
    {
        float speedToReach = 0f;
        float referenceTime = 0f;
        switch (_motorState)
        {
            case MotorState.ACCELERATING:
                speedToReach = _maxSpeed / 3.6f * _accelerationInput;
                referenceTime = _accelerationTime;
                break;
            case MotorState.BRAKING:
                speedToReach = 0f;
                referenceTime = _brakeTime;
                break;
            case MotorState.REVERSING:
                speedToReach = _maxReverseSpeed / 3.6f * _accelerationInput;
                referenceTime = _accelerationTime;
                break;
            case MotorState.IDLE:
                speedToReach = 0f;
                referenceTime = _decelerationTime;
                break;
        }
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, speedToReach, ref _smoothDampCurrentState, Mathf.Abs(speedToReach - _currentSpeed) * referenceTime / 100f);

        transform.Translate(Vector3.forward * _currentSpeed * Time.deltaTime);
    }

    private void UpdateSteering()
    {
        float steeringAngle = _maxSteeringAngle * _steeringInput;
        float speedFactor = 1f;
        if (Mathf.Approximately(_currentSpeed, 0f))
        {
            speedFactor = 0f;
        }
        else if (_currentSpeed < 0f)
        {
            speedFactor = -speedFactor;
        }

        transform.Rotate(Vector3.up, steeringAngle * speedFactor * Time.deltaTime);

        foreach (var wheel in _steeringWheels)
        {
            wheel.localRotation = Quaternion.AngleAxis(steeringAngle, Vector3.up);
        }
    }

    private void UpdateRearLight()
    {
        switch (_motorState)
        {
            case MotorState.REVERSING:
                _rearLight.gameObject.SetActive(true);
                _rearLight.material.SetColor("_EmissionColor", _reverseLightColor);
                break;
            case MotorState.BRAKING:
                _rearLight.gameObject.SetActive(true);
                _rearLight.material.SetColor("_EmissionColor", _brakeLightColor);
                break;
            default:
                _rearLight.gameObject.SetActive(false);
                break;
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

    private void UpdateCamera()
    {
        _vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>().m_AmplitudeGain = Mathf.Abs(_currentSpeed / _maxSpeed);
    }
#endregion
}
