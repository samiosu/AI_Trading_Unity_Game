
using TMPro;
using UnityEngine;

public class Amount:MonoBehaviour
{
    public int StockAmount = 0;
    [SerializeField] private TMP_InputField inputField;

    public void OnButtonChanged(int delta)
    {
        StockAmount += delta;
        if(StockAmount < 0)
        {
            StockAmount = 0;
        }
        inputField.SetTextWithoutNotify($"{StockAmount}");
    }
    // 入力による変更
    public void OnTextChanged()
    {
        int textAmount = 0;
        string text = inputField.text;
        Debug.Log("root");
        if(int.TryParse(text, out int result))
        {
            textAmount = result;
        }
        else
        {
            inputField.text = "";
            StockAmount = 0;
            return;
        }

        if(textAmount < 0)
        {
            textAmount = 0;
        }
        // 100の倍数に数値を成形する
        textAmount = textAmount / 100 * 100;
        inputField.text = $"{textAmount}";
        Debug.Log($"{textAmount}");
        StockAmount = textAmount;
    }

    public void ResetAmount()
    {
        inputField.text = "";
        StockAmount = 0;
    }
}