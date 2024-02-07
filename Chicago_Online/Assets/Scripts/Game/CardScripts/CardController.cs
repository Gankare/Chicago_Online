using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardController : MonoBehaviour
{
    public GameObject card;
    public List<CardScriptableObject> cards = new();
    private float offset;
    void Start()
    {
       for (int i = 0; i < cards.Count; i++)
       {
            var currentCard = Instantiate(card, transform);
            currentCard.GetComponent<SpriteRenderer>().sprite = cards[i].cardSprite;
            currentCard.GetComponent<CardInfo>().power = cards[i].power;
            currentCard.transform.position = new Vector2(-7 + offset, 0);
            offset += 0.2f;
        } 
    }
}
