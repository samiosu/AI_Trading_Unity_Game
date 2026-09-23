using UnityEngine;

public class AmountDown:MonoBehaviour
{
    [SerializeField] private Amount amount;
    public void OnButton()
    {
        amount.OnButtonChanged(-100);
    }
}