using UnityEngine;
using XCharts.Runtime;
using System.Collections.Generic;
using System.Collections;


public class LineChartTest : MonoBehaviour
{
    public LineChart chart;
    void Start()
    {
        chart.ClearData();
        for (int i = 0; i < 20; i++)
        {
            chart.AddXAxisData("x" + i);
            chart.AddData(0, Random.Range(10, 20));
        }
    }
}
