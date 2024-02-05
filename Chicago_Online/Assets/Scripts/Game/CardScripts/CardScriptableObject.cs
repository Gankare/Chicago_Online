using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Card")]
public class CardScriptableObject : ScriptableObject
{
    public enum Suit
    {
        Clubs = 0, Diamonds = 1, Hearts = 2, Spades = 3
    }
    public enum CardHierarchy
    {
        two = 2,
        three = 3,
        four= 4,
        five = 5,
        six = 6,
        seven = 7,
        eight = 8,
        nine = 9,
        ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }

    public Suit cardSuit;
    public CardHierarchy cardNumber;
    public int power;
    public Sprite cardSprite;
    public string cardId;
    public void Awake()
    {
        power = (int)cardNumber;
        cardId = cardSuit.ToString() + cardNumber.ToString();
    }
}
