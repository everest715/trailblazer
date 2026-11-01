/////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2024 PICO Developer
// SPDX-License-Identifier: MIT
////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.Events;
using System;

namespace MotionTracking
{
    public class MotionTrackerSampler : MonoBehaviour
    {
        public TrackerSN id;
        public long trackerId = -1;
        [HideInInspector]
        public int motionTrakcerIndex;
        [HideInInspector]
        public bool isReadJson;
        [HideInInspector]
        public bool isBindAss;
        [HideInInspector]
        public UnityAction<bool> trackerConfidenceAction;
        [HideInInspector]
        public UnityAction<bool> trackerConfidenceActionPen;
        public GameObject textSN;
        [HideInInspector]
        public Transform _transform;
        [HideInInspector]
        public Vector3 jsonOffestTrackerPostion;
        [HideInInspector]
        public Vector3 jsonOffestTrackerRotation;
        [HideInInspector]
        public Vector3 startPostion;
        [HideInInspector]
        public Quaternion startRotation;
        [SerializeField]
        private Vector3 testRow;
        [SerializeField]
        private Vector3 testPOS;
        private MotionTrackerLocation _location;
        private bool _isValidPose;
        [SerializeField]
        private GameObject particle;
        [SerializeField]
        private Material material;
        [SerializeField]
        private Material materialAlph;
        private string objSN;
        [SerializeField]
        private GameObject motionTracker;
        private bool isTrackerAction;

        private void Awake()
        {
            _transform = transform;
            PXR_MotionTracking.MotionTrackerPowerKeyAction += MotionTrackerPowerKeyAction;
        }

        private void OnDestroy()
        {
            PXR_MotionTracking.MotionTrackerPowerKeyAction -= MotionTrackerPowerKeyAction;
        }

        private void MotionTrackerPowerKeyAction(long eventTrackerId, bool pressed)
        {
            if (!pressed || eventTrackerId != trackerId)
            {
                return;
            }

            if (particle != null)
            {
                particle.SetActive(true);
                particle.GetComponentInChildren<ParticleSystem>().Play();
            }

            isTrackerAction = !isTrackerAction;
            trackerConfidenceActionPen?.Invoke(isTrackerAction);
        }

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        private void Start()
        {
            motionTracker.GetComponent<MeshRenderer>().materials[0] = material;
            if (TryGetPose(out var position, out var rotation))
            {
                startPostion = position;
                startRotation = rotation;
            }
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(id.value) || trackerId < 0)
            {
                return;
            }

            if (!TryGetPose(out var predictPosition, out var predictRotation))
            {
                if (!isBindAss)
                {
                    motionTracker.GetComponent<Renderer>().material = materialAlph;
                    trackerConfidenceAction?.Invoke(false);
                }
                else
                {
                    motionTracker.GetComponent<Renderer>().material = materialAlph;
                }
                return;
            }

            if (!isBindAss)
            {
                motionTracker.GetComponent<Renderer>().material = material;
                trackerConfidenceAction?.Invoke(true);
            }
            else
            {
                motionTracker.GetComponent<Renderer>().material = material;
            }

            startPostion = predictPosition;
            startRotation = predictRotation;
            if (!isReadJson)
            {
                _transform.position = predictPosition;
                _transform.rotation = predictRotation;
            }
            else
            {
                _transform.rotation = Quaternion.Euler(predictRotation.eulerAngles + jsonOffestTrackerRotation);
                _transform.position = predictPosition + jsonOffestTrackerPostion;
            }
        }

        private bool TryGetPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (trackerId < 0)
            {
                return false;
            }

            _isValidPose = false;
            var result = PXR_MotionTracking.GetMotionTrackerLocation(trackerId, ref _location, ref _isValidPose);
            if (result != 0 || !_isValidPose)
            {
                return false;
            }

            position = _location.pose.Position.ToVector3();
            rotation = _location.pose.Orientation.ToQuat();
            return true;
        }

        private void OnBeforeRender()
        {
            Update();
        }
    }
}
