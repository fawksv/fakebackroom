using UnityEngine;
using UnityEngine.UI;

namespace PlayerPrototype
{
    public sealed class SettingsController : MonoBehaviour
    {
        private const string MasterVolumeKey = "PlayerPrototype.MasterVolume";
        private const string MouseSensitivityKey = "PlayerPrototype.MouseSensitivity";

        private const float DefaultMasterVolume = 1f;
        private const float DefaultMouseSensitivity = 2f;

        private const float MinVolume = 0f;
        private const float MaxVolume = 1f;
        private const float MinSensitivity = 0.5f;
        private const float MaxSensitivity = 10f;

        [Header("Volume")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValueText;

        [Header("Mouse Sensitivity")]
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValueText;

        [Header("Optional Player Reference")]
        [SerializeField] private PlayerController playerController;

        private bool listenersRegistered;
        private bool hasVolumeControls;
        private bool hasSensitivityControls;

        private void Awake()
        {
            hasVolumeControls = ValidateVolumeControls();
            hasSensitivityControls = ValidateSensitivityControls();

            if (hasVolumeControls)
            {
                ConfigureVolumeSlider();
                float volume = Mathf.Clamp(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume), MinVolume, MaxVolume);
                volumeSlider.SetValueWithoutNotify(volume);
                ApplyVolume(volume);
                UpdateVolumeText(volume);
            }

            if (hasSensitivityControls)
            {
                ConfigureSensitivitySlider();
                float sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity), MinSensitivity, MaxSensitivity);
                sensitivitySlider.SetValueWithoutNotify(sensitivity);
                ApplySensitivity(sensitivity);
                UpdateSensitivityText(sensitivity);
            }

            RegisterListeners();
        }

        private void OnDestroy()
        {
            UnregisterListeners();
            PlayerPrefs.Save();
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.Save();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                PlayerPrefs.Save();
            }
        }

        private void OnVolumeChanged(float value)
        {
            float volume = Mathf.Clamp(value, MinVolume, MaxVolume);
            ApplyVolume(volume);
            UpdateVolumeText(volume);
            PlayerPrefs.SetFloat(MasterVolumeKey, volume);
        }

        private void OnSensitivityChanged(float value)
        {
            float sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
            ApplySensitivity(sensitivity);
            UpdateSensitivityText(sensitivity);
            PlayerPrefs.SetFloat(MouseSensitivityKey, sensitivity);
        }

        private bool ValidateVolumeControls()
        {
            bool valid = true;

            if (volumeSlider == null)
            {
                Debug.LogError("SettingsController is missing the Volume Slider reference.", this);
                valid = false;
            }

            if (volumeValueText == null)
            {
                Debug.LogError("SettingsController is missing the Volume Value Text reference.", this);
                valid = false;
            }

            return valid;
        }

        private bool ValidateSensitivityControls()
        {
            bool valid = true;

            if (sensitivitySlider == null)
            {
                Debug.LogError("SettingsController is missing the Sensitivity Slider reference.", this);
                valid = false;
            }

            if (sensitivityValueText == null)
            {
                Debug.LogError("SettingsController is missing the Sensitivity Value Text reference.", this);
                valid = false;
            }

            return valid;
        }

        private void ConfigureVolumeSlider()
        {
            volumeSlider.minValue = MinVolume;
            volumeSlider.maxValue = MaxVolume;
            volumeSlider.wholeNumbers = false;
        }

        private void ConfigureSensitivitySlider()
        {
            sensitivitySlider.minValue = MinSensitivity;
            sensitivitySlider.maxValue = MaxSensitivity;
            sensitivitySlider.wholeNumbers = false;
        }

        private void RegisterListeners()
        {
            if (listenersRegistered)
            {
                return;
            }

            if (hasVolumeControls)
            {
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }

            if (hasSensitivityControls)
            {
                sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            }

            listenersRegistered = true;
        }

        private void UnregisterListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            }

            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
            }

            listenersRegistered = false;
        }

        private void ApplyVolume(float value)
        {
            AudioListener.volume = value;
        }

        private void ApplySensitivity(float value)
        {
            if (playerController != null)
            {
                playerController.mouseSensitivity = Mathf.Clamp(value, 0.5f, 10f);
            }
        }

        private void UpdateVolumeText(float value)
        {
            volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        private void UpdateSensitivityText(float value)
        {
            sensitivityValueText.text = value.ToString("0.0");
        }
    }
}
