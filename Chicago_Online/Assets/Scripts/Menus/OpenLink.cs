using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenLink : MonoBehaviour
{
    private string chicagoRulesLink = "https://www.pagat.com/last/chicago.html";

    public void OpenRules()
    {
        Application.OpenURL(chicagoRulesLink);
    }
}
