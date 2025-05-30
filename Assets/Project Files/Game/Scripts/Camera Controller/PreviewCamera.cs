﻿using System.Collections.Generic;
using UnityEngine;

namespace Watermelon
{
    public static class PreviewCamera
    {
        private const float CAMERA_MOVEMENT_SPEED = 40;

        private static VirtualCamera previewCamera;
        private static GameObject previewCameraTarget;

        private static bool isActive;
        public static bool IsActive => isActive;

        private static bool isPaused;
        private static TweenCase mainTweenCase;

        private static Queue<CameraCase> waitingCases = new Queue<CameraCase>();
        private static Queue<CameraCase> finishedCases = new Queue<CameraCase>();

        private static CameraCase frozenCase;

        public static void Initialise()
        {
            // Get tutorial virtual camera
            previewCamera = CameraController.GetCamera(CameraType.Preview);

            // Create tutorial camera target
            previewCameraTarget = new GameObject("[TUTORIAL CAMERA TARGET]");
            previewCameraTarget.transform.ResetGlobal();
        }

        public static void ResetTargetPosition()
        {
            VirtualCamera mainCamera = CameraController.GetCamera(CameraType.Gameplay);

            // Reset camera position
            previewCameraTarget.transform.position = mainCamera.Target.position;
            Debug.Log("Prev: " + mainCamera.Target.position);
        }

        public static void SetTargetPosition(Vector3 position)
        {
            previewCameraTarget.transform.position = position;
        }

        public static void Focus(Vector3 targetPosition, float freezeTime, SimpleCallback onStart = null, SimpleCallback onFocused = null, SimpleCallback onFreezeTimeEnded = null, SimpleCallback onFinished = null, bool debug = false)
        {
            if (CameraController.IsBlending) return;

            if(debug)
            {
                onStart?.Invoke();

                Tween.NextFrame(delegate
                {
                    onFocused?.Invoke();
                    onFreezeTimeEnded?.Invoke();
                    onFinished?.Invoke();

                    Control.EnableMovementControl();
                });

                return;
            }

            CameraCase cameraCase = new CameraCase(targetPosition, freezeTime, onStart, onFocused, onFreezeTimeEnded, onFinished);

            if(!isActive)
            {
                isActive = true;

                VirtualCamera mainCamera = CameraController.GetCamera(CameraType.Gameplay);

                // Reset camera position
                previewCameraTarget.transform.position = mainCamera.Target.position;

                // Disable player joystick
                Control.DisableMovementControl();

                // Start camera movement
                InvokeCase(cameraCase);
            }
            else
            {
                // Add camera case to queue
                waitingCases.Enqueue(cameraCase);
            }
        }

        private static void InvokeCase(CameraCase cameraCase)
        {
            VirtualCamera previewCamera = CameraController.GetCamera(CameraType.Preview);
            previewCamera.SetTarget(previewCameraTarget.transform);

            // Enable Cinemachine tutorial camera
            CameraController.EnableCamera(CameraType.Preview);

            // Invoke camera case start callback
            cameraCase.onStart?.Invoke();

            if (cameraCase.freezeTime < 0)
                frozenCase = cameraCase;

#if MODULE_CURVE
            CurvatureManager.EnableTempTarget(previewCameraTarget.transform);
#endif

            // Start target movement
            mainTweenCase = previewCameraTarget.transform.DOMove(cameraCase.targetPosition, Mathf.Clamp(Vector3.Distance(cameraCase.targetPosition, previewCameraTarget.transform.position) / CAMERA_MOVEMENT_SPEED, 0.4f, float.MaxValue), 0, false, UpdateMethod.Update).SetEasing(Ease.Type.CubicInOut).OnComplete(() =>
            {
                // Invoke camera case focused callback
                cameraCase.onFocused?.Invoke();

                if (cameraCase.freezeTime >= 0)
                    Tween.DelayedCall(cameraCase.freezeTime, () => UnfreezeCase(cameraCase));
            });
        }

        public static void Unfreeze()
        {
            UnfreezeCase(frozenCase);
        }

        private static void UnfreezeCase(CameraCase cameraCase)
        {
            VirtualCamera mainCamera = CameraController.GetCamera(CameraType.Gameplay);

            if (waitingCases.Count == 0)
            {
                cameraCase.onFreezeTimeEnded?.Invoke();

                mainTweenCase = previewCameraTarget.transform.DOMove(mainCamera.Target.position, Mathf.Clamp(Vector3.Distance(mainCamera.Target.position, previewCameraTarget.transform.position) / CAMERA_MOVEMENT_SPEED, 0.4f, float.MaxValue), 0, false, UpdateMethod.Update).SetEasing(Ease.Type.CubicInOut).OnComplete(() =>
                {
                    CameraController.EnableCamera(CameraType.Gameplay);

                    // Invoke camera case finished callback
                    cameraCase.onFinished?.Invoke();

                    while (finishedCases.Count > 0)
                    {
                        CameraCase finishedCase = finishedCases.Dequeue();
                        if (finishedCase != null)
                        {
                            finishedCase.onFinished?.Invoke();
                        }
                    }

                    if (waitingCases.Count > 0)
                    {
                        CameraCase nextCase = waitingCases.Dequeue();

                        InvokeCase(nextCase);
                    }
                    else
                    {
                        isActive = false;

                        Control.EnableMovementControl();

#if MODULE_CURVE
                        CurvatureManager.DisableTempTarget();
#endif
                    }
                });
            }
            else
            {
                var nextCase = waitingCases.Dequeue();

                finishedCases.Enqueue(cameraCase);

                InvokeCase(nextCase);
            }

            if (isPaused)
            {
                if (mainTweenCase != null)
                    mainTweenCase.Pause();
            }
        }

        public static void Unload()
        {
            isActive = false;

            frozenCase = null;

            waitingCases.Clear();
            finishedCases.Clear();

            // Enable Cinemachine main camera
            CameraController.EnableCamera(CameraType.Gameplay);
        }

        public static void Pause()
        {
            isPaused = true;

            if (mainTweenCase != null)
                mainTweenCase.Pause();
        }

        public static void Resume()
        {
            isPaused = false;

            if (mainTweenCase != null)
                mainTweenCase.Resume();
        }

        private class CameraCase
        {
            public Vector3 targetPosition;

            public float freezeTime;

            public SimpleCallback onStart;
            public SimpleCallback onFocused;
            public SimpleCallback onFreezeTimeEnded;
            public SimpleCallback onFinished;

            public CameraCase(Vector3 targetPosition, float freezeTime, SimpleCallback onStart = null, SimpleCallback onFocused = null, SimpleCallback onFreezeTimeEnded = null, SimpleCallback onFinished = null)
            {
                this.targetPosition = targetPosition;
                this.freezeTime = freezeTime;

                this.onStart = onStart;
                this.onFocused = onFocused;
                this.onFreezeTimeEnded = onFreezeTimeEnded;
                this.onFinished = onFinished;
            }
        }
    }
}