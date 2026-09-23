using UnityEngine;

public class AmountUp:MonoBehaviour
{
    [SerializeField] private Amount amount;
    public void OnButton()
    {
        amount.OnButtonChanged(100);
    }
}