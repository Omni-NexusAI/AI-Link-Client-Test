using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using TMPro;
using LazyFollow = UnityEngine.XR.Interaction.Toolkit.UI.LazyFollow;

namespace UnityEngine.XR.Templates.MR
{
    public struct Goal
    {
        public GoalManager.OnboardingGoals CurrentGoal;
        public bool Completed;

        public Goal(GoalManager.OnboardingGoals goal)
        {
            CurrentGoal = goal;
            Completed = false;
        }
    }

    public class GoalManager : MonoBehaviour
    {
        public enum OnboardingGoals
        {
            Empty,
            FindSurfaces,
            TapSurface,
        }

        Queue<Goal> m_OnboardingGoals;
        Goal m_CurrentGoal;
        bool m_AllGoalsFinished;
        int m_SurfacesTapped;
        int m_CurrentGoalIndex = 0;

        [Serializable]
        class Step
        {
            [SerializeField]
            public GameObject stepObject;

            [SerializeField]
            public string buttonText;

            public bool includeSkipButton;
        }

        [SerializeField]
        List<Step> m_StepList = new List<Step>();

        [SerializeField]
        public TextMeshProUGUI m_StepButtonTextField;

        [SerializeField]
        public GameObject m_SkipButton;

        [SerializeField]
        GameObject m_LearnButton;

        [SerializeField]
        GameObject m_LearnModal;

        [SerializeField]
        Button m_LearnModalButton;

        [SerializeField]
        GameObject m_CoachingUIParent;

        [SerializeField]
        FadeMaterial m_FadeMaterial;

        [SerializeField]
        Toggle m_PassthroughToggle;

        [SerializeField]
        LazyFollow m_GoalPanelLazyFollow;

        [SerializeField]
        GameObject m_TapTooltip;

        [SerializeField]
        GameObject m_VideoPlayer;

        [SerializeField]
        Toggle m_VideoPlayerToggle;

        [SerializeField]
        ARFeatureController m_FeatureController;

        [SerializeField]
        ObjectSpawner m_ObjectSpawner;

        const int k_NumberOfSurfacesTappedToCompleteGoal = 1;
        Vector3 m_TargetOffset = new Vector3(-.5f, -.25f, 1.5f);

        void Start()
        {
            // Skip the coaching UI and go directly to the simulated environment
            if (m_CoachingUIParent != null)
            {
                m_CoachingUIParent.transform.localScale = Vector3.zero;
            }

            if (m_TapTooltip != null)
                m_TapTooltip.SetActive(false);

            if (m_VideoPlayer != null)
            {
                m_VideoPlayer.SetActive(false);

                if (m_VideoPlayerToggle != null)
                    m_VideoPlayerToggle.isOn = false;
            }

            if (m_FadeMaterial != null)
            {
                m_FadeMaterial.FadeSkybox(true);

                if (m_PassthroughToggle != null)
                    m_PassthroughToggle.isOn = true;
            }

            if (m_LearnButton != null)
            {
                m_LearnButton.GetComponent<Button>().onClick.AddListener(OpenModal);
                m_LearnButton.SetActive(false);
            }

            if (m_LearnModal != null)
            {
                m_LearnModal.transform.localScale = Vector3.zero;
            }

            if (m_LearnModalButton != null)
            {
                m_LearnModalButton.onClick.AddListener(CloseModal);
            }

            if (m_ObjectSpawner == null)
            {
#if UNITY_2023_1_OR_NEWER
                m_ObjectSpawner = FindAnyObjectByType<ObjectSpawner>();
#else
                m_ObjectSpawner = FindObjectOfType<ObjectSpawner>();
#endif
            }

            if (m_FeatureController == null)
            {
#if UNITY_2023_1_OR_NEWER
                m_FeatureController = FindAnyObjectByType<ARFeatureController>();
#else
                m_FeatureController = FindObjectOfType<ARFeatureController>();
#endif
            }

            // Initialize the environment directly
            m_AllGoalsFinished = true;
            StartCoroutine(TurnOnARFeatures());
        }

        void OpenModal()
        {
            if (m_LearnModal != null)
            {
                m_LearnModal.transform.localScale = Vector3.one;
            }
        }

        void CloseModal()
        {
            if (m_LearnModal != null)
            {
                m_LearnModal.transform.localScale = Vector3.zero;
            }
        }

        void Update()
        {
            if (!m_AllGoalsFinished)
            {
                ProcessGoals();
            }

            // Debug Input
#if UNITY_EDITOR
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                CompleteGoal();
            }
#endif
        }

        void ProcessGoals()
        {
            if (!m_CurrentGoal.Completed)
            {
                switch (m_CurrentGoal.CurrentGoal)
                {
                    case OnboardingGoals.Empty:
                        m_GoalPanelLazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
                        break;
                    case OnboardingGoals.FindSurfaces:
                        m_GoalPanelLazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
                        break;
                    case OnboardingGoals.TapSurface:
                        if (m_TapTooltip != null)
                        {
                            m_TapTooltip.SetActive(true);
                        }
                        m_GoalPanelLazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.None;
                        break;
                }
            }
        }

        void CompleteGoal()
        {
            if (m_CurrentGoal.CurrentGoal == OnboardingGoals.TapSurface)
                m_ObjectSpawner.objectSpawned -= OnObjectSpawned;

            // disable tooltips before setting next goal
            DisableTooltips();

            m_CurrentGoal.Completed = true;
            m_CurrentGoalIndex++;
            if (m_OnboardingGoals.Count > 0)
            {
                m_CurrentGoal = m_OnboardingGoals.Dequeue();
                m_StepList[m_CurrentGoalIndex - 1].stepObject.SetActive(false);
                m_StepList[m_CurrentGoalIndex].stepObject.SetActive(true);
                m_StepButtonTextField.text = m_StepList[m_CurrentGoalIndex].buttonText;
                m_SkipButton.SetActive(m_StepList[m_CurrentGoalIndex].includeSkipButton);
            }
            else
            {
                m_AllGoalsFinished = true;
                ForceEndAllGoals();
            }

            if (m_CurrentGoal.CurrentGoal == OnboardingGoals.FindSurfaces)
            {
                if (m_FadeMaterial != null)
                    m_FadeMaterial.FadeSkybox(true);

                if (m_PassthroughToggle != null)
                    m_PassthroughToggle.isOn = true;

                if (m_LearnButton != null)
                {
                    m_LearnButton.SetActive(true);
                }

                StartCoroutine(TurnOnPlanes(true));
            }
            else if (m_CurrentGoal.CurrentGoal == OnboardingGoals.TapSurface)
            {
                if (m_LearnButton != null)
                {
                    m_LearnButton.SetActive(false);
                }
                m_SurfacesTapped = 0;
                m_ObjectSpawner.objectSpawned += OnObjectSpawned;
            }
        }

        public IEnumerator TurnOnPlanes(bool visualize)
        {
            yield return new WaitForSeconds(1f);

            if (m_FeatureController != null)
            {
                m_FeatureController.TogglePlaneVisualization(visualize);
                m_FeatureController.TogglePlanes(true);
            }
        }

        IEnumerator TurnOnARFeatures()
        {
            if (m_FeatureController == null)
                yield return null;

            yield return new WaitForSeconds(0.5f);

            // We are checking the bounding box count here so that we disable plane visuals so there is no
            // visual Z fighting. If the user has not defined any furniture in space setup or the platform
            // does not support bounding boxes, we want to enable plane visuals, but disable bounding box visuals.
            m_FeatureController.ToggleBoundingBoxes(true);
            m_FeatureController.TogglePlanes(true);

            // Quick hack for for async await race condition.
            // TODO: -- Probably better to listen to trackable change events in the ARFeatureController and update accordingly there
            yield return new WaitForSeconds(0.5f);
            m_FeatureController.ToggleDebugInfo(false);

            // If there are bounding boxes, we want to hide the planes so they don't cause z-fighting.
            if (m_FeatureController.HasBoundingBoxes())
            {
                m_FeatureController.TogglePlaneVisualization(false);
                m_FeatureController.ToggleBoundingBoxVisualization(true);
            }
            else
            {
                m_FeatureController.ToggleBoundingBoxVisualization(true);
            }

            m_FeatureController.occlusionManager.SetupManager();
        }

        void TurnOffARFeatureVisualization()
        {
            if (m_FeatureController == null)
                return;

            m_FeatureController.TogglePlaneVisualization(false);
            m_FeatureController.ToggleBoundingBoxVisualization(false);
        }

        void DisableTooltips()
        {
            if (m_CurrentGoal.CurrentGoal == OnboardingGoals.TapSurface)
            {
                if (m_TapTooltip != null)
                {
                    m_TapTooltip.SetActive(false);
                }
            }
        }

        public void ForceCompleteGoal()
        {
            CompleteGoal();
        }

        public void ForceEndAllGoals()
        {
            m_CoachingUIParent.transform.localScale = Vector3.zero;

            TurnOnVideoPlayer();

            if (m_VideoPlayerToggle != null)
                m_VideoPlayerToggle.isOn = true;

            if (m_FadeMaterial != null)
            {
                m_FadeMaterial.FadeSkybox(true);

                if (m_PassthroughToggle != null)
                    m_PassthroughToggle.isOn = true;
            }

            if (m_LearnButton != null)
            {
                m_LearnButton.SetActive(false);
            }

            if (m_LearnModal != null)
            {
                m_LearnModal.transform.localScale = Vector3.zero;
            }

            StartCoroutine(TurnOnARFeatures());
        }

        public void ResetCoaching()
        {
            // Skip coaching UI and go directly to the simulated environment
            if (m_CoachingUIParent != null)
            {
                m_CoachingUIParent.transform.localScale = Vector3.zero;
            }

            if (m_TapTooltip != null)
                m_TapTooltip.SetActive(false);

            if (m_LearnButton != null)
            {
                m_LearnButton.SetActive(false);
            }

            if (m_LearnModal != null)
            {
                m_LearnModal.transform.localScale = Vector3.zero;
            }

            // Initialize the environment directly
            m_AllGoalsFinished = true;
            StartCoroutine(TurnOnARFeatures());
        }

        void OnObjectSpawned(GameObject spawnedObject)
        {
            m_SurfacesTapped++;
            if (m_CurrentGoal.CurrentGoal == OnboardingGoals.TapSurface && m_SurfacesTapped >= k_NumberOfSurfacesTappedToCompleteGoal)
            {
                CompleteGoal();
                m_GoalPanelLazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
            }
        }

        public void TooglePlayer(bool visibility)
        {
            if (visibility)
            {
                TurnOnVideoPlayer();
            }
            else
            {
                if (m_VideoPlayer.activeSelf)
                {
                    m_VideoPlayer.SetActive(false);
                    if (m_VideoPlayerToggle.isOn)
                        m_VideoPlayerToggle.isOn = false;
                }
            }
        }

        void TurnOnVideoPlayer()
        {
            if (m_VideoPlayer.activeSelf)
                return;

            var follow = m_VideoPlayer.GetComponent<LazyFollow>();
            if (follow != null)
                follow.rotationFollowMode = LazyFollow.RotationFollowMode.None;

            m_VideoPlayer.SetActive(false);
            var target = Camera.main.transform;
            var targetRotation = target.rotation;
            var newTransform = target;
            var targetEuler = targetRotation.eulerAngles;
            targetRotation = Quaternion.Euler
            (
                0f,
                targetEuler.y,
                targetEuler.z
            );

            newTransform.rotation = targetRotation;
            var targetPosition = target.position + newTransform.TransformVector(m_TargetOffset);
            m_VideoPlayer.transform.position = targetPosition;

            var forward = target.position - m_VideoPlayer.transform.position;
            var targetPlayerRotation = forward.sqrMagnitude > float.Epsilon ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
            targetPlayerRotation *= Quaternion.Euler(new Vector3(0f, 180f, 0f));
            var targetPlayerEuler = targetPlayerRotation.eulerAngles;
            var currentEuler = m_VideoPlayer.transform.rotation.eulerAngles;
            targetPlayerRotation = Quaternion.Euler
            (
                currentEuler.x,
                targetPlayerEuler.y,
                currentEuler.z
            );

            m_VideoPlayer.transform.rotation = targetPlayerRotation;
            m_VideoPlayer.SetActive(true);
            if (follow != null)
                follow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
        }
    }
}
