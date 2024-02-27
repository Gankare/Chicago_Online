using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardButtonClick : MonoBehaviour
{
    public GameController gameController;

    void Start()
    {
        gameController = FindObjectOfType<GameController>();
    }

    public void SelectCard()
    {
        if (gameController != null)
        {
            gameController.SelectCardToThrow(this.gameObject);
        }
        else
        {
            Debug.LogError("GameController script is not found in the scene.");
        }
    }
}
