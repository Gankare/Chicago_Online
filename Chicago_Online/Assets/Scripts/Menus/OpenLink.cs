using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenLink : MonoBehaviour
{
    private string chicagoRulesLink = "https://www.pagat.com/last/chicago.html";

    public void OpenRules()
    {
        // Open the Google link in the default browser
        Application.OpenURL(chicagoRulesLink);
    }
}
