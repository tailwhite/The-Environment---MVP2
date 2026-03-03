using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mesh;

public class 测试脚本 : MonoBehaviour
{
    public TestData data;

    [TextArea]
    public string text;
}

[System.Serializable]
public class TestData
{
    public string name;
    public int value;
}