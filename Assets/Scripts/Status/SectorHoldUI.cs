using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class SectorHoldUI : MonoBehaviour
{
    [SerializeField] TMP_Text amount;
    [SerializeField] TMP_Text costPerUnit;
    [SerializeField] TMP_Text price;
    [SerializeField] TMP_Text profitAndLoss;
    [SerializeField] TMP_Text profitAndLossRation;
    private GlobalStatus globalStatus;
    private NextDayEvent nextDayEvent;
    
    public void Awake()
    {
        globalStatus = GlobalStatus.GetInstance();
        nextDayEvent = NextDayEvent.GetInstance();
    }
    public void OnEnable()
    {
        globalStatus.IdChanged += UpdateUI;
        nextDayEvent.nextDay += UpdateUI;
    }
    public void OnDisable()
    {
        globalStatus.IdChanged -= UpdateUI;
        nextDayEvent.nextDay -= UpdateUI;
    }

    public void UpdateUI()
    {
        StockHoldings unit = globalStatus.StockHoldings[globalStatus.SectorId];
        amount.SetText($"{unit.Amount}");
        costPerUnit.SetText($"{unit.CostPerUnit}");
        price.SetText($"{unit.Price}");
        profitAndLoss.SetText($"{unit.ProfitAndLoss}");
        profitAndLossRation.SetText($"{unit.ProfitAndLossRation:F2}%");
    }
}