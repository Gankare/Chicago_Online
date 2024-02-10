using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardButtonClick : MonoBehaviour
{
    public GameController gameController;

    // Start is called before the first frame update
    void Start()
    {
        // Find the GameController script in the scene
        gameController = FindObjectOfType<GameController>();

        // Get the Button component attached to this GameObject
        Button button = GetComponent<Button>();

        // Add a listener for the button click event
        button.onClick.AddListener(ThrowCard);
    }

    // Function to throw the card when the button is clicked
    void ThrowCard()
    {
        // Check if the GameController script is found
        if (gameController != null)
        {
            // Call the SelectCardToThrow function from the GameController script
            gameController.SelectCardToThrow(this.gameObject);
        }
        else
        {
            Debug.LogError("GameController script is not found in the scene.");
        }
    }
}
