using UnityEngine;
using TMPro;

public class CoreEnergyUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI energyText;

    private EnergyBeam energyBeam;
    private float lastKnownEnergy = -1f;

    void Start()
    {
        if (energyText == null)
        {
            Debug.LogError("[CoreEnergyUI] Energy text not assigned!");
        }
    }

    void Update()
    {
        // Try to find energy beam if we don't have one
        if (energyBeam == null)
        {
            energyBeam = FindObjectOfType<EnergyBeam>();
        }

        // Update UI with current energy
        if (energyBeam != null)
        {
            float currentEnergy = energyBeam.GetCurrentEnergy();
            float maxEnergy = energyBeam.GetMaxEnergy();

            // Only update if energy changed (optimization)
            if (currentEnergy != lastKnownEnergy)
            {
                UpdateUI(currentEnergy, maxEnergy);
                lastKnownEnergy = currentEnergy;
            }
        }
        else
        {
            // Show waiting message if no beam found yet
            if (energyText != null && lastKnownEnergy != -1f)
            {
                energyText.text = "Core Energy: --";
                lastKnownEnergy = -1f;
            }
        }
    }

    void UpdateUI(float currentEnergy, float maxEnergy)
    {
        if (energyText != null)
        {
            energyText.text = $"Core Energy: {currentEnergy:F2} / {maxEnergy:F0}";
        }
    }
}
