using UnityEngine;
using UnityEngine.UI;

public class WaterTankUI : MonoBehaviour
{
    [Header("Water Storage")]
    [SerializeField] private float maxWaterStorage = 100f;
    [SerializeField] private float currentWaterStorage;

    [Header("UI References")]
    [SerializeField] private Image tankFillImage;
    [SerializeField] private Text storageText;

    [Header("Consumption Settings")]
    [SerializeField] private float consumptionAmount = 10f;

    private void Start()
    {
        // Initialize current water storage
        currentWaterStorage = maxWaterStorage;
        UpdateUI();
    }

    private void Update()
    {
        // Consume water when the left mouse button is clicked
        if (Input.GetMouseButtonDown(0))
        {
            ConsumeWater();
        }
    }

    public void ConsumeWater()
    {
        // Reduce current water storage, ensuring it doesn't drop below zero
        currentWaterStorage = Mathf.Max(0, currentWaterStorage - consumptionAmount);

        // Update the UI to reflect the change
        UpdateUI();
    }

    private void UpdateUI()
    {
        // Update the water tank fill amount
        if (tankFillImage != null)
        {
            tankFillImage.fillAmount = currentWaterStorage / maxWaterStorage;
        }

        // Update the text to display the current and maximum storage
        if (storageText != null)
        {
            storageText.text = $"{Mathf.RoundToInt(currentWaterStorage)}/{Mathf.RoundToInt(maxWaterStorage)}";
        }
    }
}
