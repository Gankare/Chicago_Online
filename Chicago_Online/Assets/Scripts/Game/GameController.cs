using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

// GameController.cs

public class GameController : MonoBehaviour
{
    public GameObject card;
    public List<CardScriptableObject> cards = new();
    public List<string> deck = new();
    public List<string> discardPile = new();
    public List<string> playersHand = new();
    private string serverId;
    private void Awake()
    {
        serverId = ServerManager.instance.serverId;
        ShuffleAndDealOwnCards(cards);
    }

    public void ShuffleAndDealOwnCards(List<CardScriptableObject> deck)
    {
        // Shuffle the deck locally
        List<CardScriptableObject> shuffledDeck = ShuffleDeck(deck);

        // Deal cards to the local player
        DealCards(shuffledDeck);


        var setServerDeck = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("cardDeck").SetValueAsync(deck);
        var setServerDiscardPile = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("discardPile").SetValueAsync(discardPile);
        var setUserHand = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").SetValueAsync(playersHand);
        yield return new WaitUntil(() => setServerDeck.IsCompleted && setServerDiscardPile.IsCompleted && setUserHand.IsCompleted);
       // Save the player's hand to the database
       SavePlayerHandToDatabase();
    }

    private List<CardScriptableObject> ShuffleDeck(List<CardScriptableObject> deck)
    {
        int n = deck.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            CardScriptableObject value = deck[k];
            deck[k] = deck[n];
            deck[n] = value;
        }

        return deck;
    }

    private void DealCards(List<CardScriptableObject> shuffledDeck)
    {
        int cardsToDeal = 5; // Adjust as needed

        for (int i = 0; i < cardsToDeal; i++)
        {
            // Draw a card from the top of the deck
            CardScriptableObject drawnCard = shuffledDeck[0];

            // Add the card to the player's hand
            playersHand.Add(drawnCard.cardId);

            // Remove the card from the deck
            shuffledDeck.RemoveAt(0);
        }
    }

    private void SavePlayerHandToDatabase()
    {
        // Save the player's hand to the database using Firebase SDK

        // Example: FirebaseDatabase.DefaultInstance.GetReference(playerHandPath).SetRawJsonValueAsync(JsonUtility.ToJson(playersHand));
    }
    public void IncrementRoundCounter(string serverId)
    {
        // Increment the round counter in the database
        string roundCounterPath = $"servers/{serverId}/roundCounter";
        // Use Firebase SDK to update the database
        // For example:
        // FirebaseDatabase.DefaultInstance.GetReference(roundCounterPath).RunTransaction(yourIncrementFunction);
    }
    IEnumerator CountAndSetValueOfHand()
    {
        // Retrieve the player's cards from the database
        List<CardScriptableObject> playerCards = GetPlayerCards();

        // Calculate the score for the player's hand
        int score = CalculateScore(playerCards);
        var setUserHandValue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("handValue").SetValueAsync(score);
        yield return new WaitUntil(() => setUserHandValue.IsCompleted);
        // Update the player's score in the database
        SavePlayerScore(score);
    }

    private List<CardScriptableObject> GetPlayerCards()
    {
        // Replace the next line with the actual code to fetch the cards from the database
        List<CardScriptableObject> playerCards = new List<CardScriptableObject>();

        return playerCards;
    }

    private int CalculateScore(List<CardScriptableObject> playerCards)
    {
        // Count occurrences of each card number and suit
        Dictionary<CardScriptableObject.CardHierarchy, int> cardNumberCount = new Dictionary<CardScriptableObject.CardHierarchy, int>();
        Dictionary<CardScriptableObject.Suit, int> cardSuitCount = new Dictionary<CardScriptableObject.Suit, int>();

        foreach (var card in playerCards)
        {
            // Count card numbers
            if (cardNumberCount.ContainsKey(card.cardNumber))
            {
                cardNumberCount[card.cardNumber]++;
            }
            else
            {
                cardNumberCount.Add(card.cardNumber, 1);
            }

            // Count card suits
            if (cardSuitCount.ContainsKey(card.cardSuit))
            {
                cardSuitCount[card.cardSuit]++;
            }
            else
            {
                cardSuitCount.Add(card.cardSuit, 1);
            }
        }

        // Check for a royal straight flush
        bool hasRoyalStraightFlush = CheckForRoyalStraightFlush(cardNumberCount.Keys.ToList(), cardSuitCount);

        if (hasRoyalStraightFlush)
        {
            return 52; // Points for a royal straight flush
        }

        // Check for a straight flush
        bool hasStraightFlush = CheckForStraightFlush(cardNumberCount.Keys.ToList(), cardSuitCount);

        if (hasStraightFlush)
        {
            return 8; // Points for a straight flush
        }

        // Check for four of a kind
        foreach (var count in cardNumberCount.Values)
        {
            if (count == 4)
            {
                return 7; // Points for four of a kind
            }
        }

        // Check for a full house (pair of 3 and pair of 2)
        bool hasPairOf2 = false;
        bool hasPairOf3 = false;

        foreach (var count in cardNumberCount.Values)
        {
            if (count == 2)
            {
                hasPairOf2 = true;
            }
            else if (count == 3)
            {
                hasPairOf3 = true;
            }
        }

        if (hasPairOf2 && hasPairOf3)
        {
            return 6; // Full house
        }

        // Check for a flush (five cards of the same suit)
        bool hasFlush = cardSuitCount.Any(pair => pair.Value >= 5);

        if (hasFlush)
        {
            return 5; // Points for a flush
        }

        // Check for a straight (five consecutive cards)
        bool hasStraight = CheckForStraight(cardNumberCount.Keys.ToList());

        if (hasStraight)
        {
            return 4; // Points for a straight
        }

        // Check for trips (three of a kind)
        foreach (var count in cardNumberCount.Values)
        {
            if (count == 3)
            {
                return 3; // Points for three of a kind
            }
        }

        // Check for two pairs
        int pairsCount = 0;
        foreach (var count in cardNumberCount.Values)
        {
            if (count == 2)
            {
                pairsCount++;
            }
        }

        if (pairsCount == 2)
        {
            return 2; // Points for two pairs
        }

        // Check for one pair
        if (pairsCount == 1)
        {
            return 1; // Points for one pair
        }

        // If no scoring combination is found, return 0 points
        return 0;
    }

    private bool CheckForStraight(List<CardScriptableObject.CardHierarchy> cardNumbers)
    {
        // Sort the card numbers in ascending order
        cardNumbers.Sort();

        // Check for a straight (five consecutive cards)
        for (int i = 1; i < cardNumbers.Count; i++)
        {
            if (cardNumbers[i] - cardNumbers[i - 1] != 1)
            {
                return false;
            }
        }

        return true;
    }

    private bool CheckForStraightFlush(List<CardScriptableObject.CardHierarchy> cardNumbers, Dictionary<CardScriptableObject.Suit, int> cardSuitCount)
    {
        // Implement logic to check for a straight flush (five consecutive cards of the same suit)
        // Return true if a straight flush is found, false otherwise

        // Check for a flush first
        bool hasFlush = cardSuitCount.Any(pair => pair.Value >= 5);

        if (!hasFlush)
        {
            return false; // Cannot have a straight flush without a flush
        }

        // Sort the card numbers in ascending order
        cardNumbers.Sort();

        // Check for a straight flush
        for (int i = 1; i < cardNumbers.Count; i++)
        {
            if (cardNumbers[i] - cardNumbers[i - 1] != 1)
            {
                return false;
            }
        }

        return true;
    }

    private bool CheckForRoyalStraightFlush(List<CardScriptableObject.CardHierarchy> cardNumbers, Dictionary<CardScriptableObject.Suit, int> cardSuitCount)
    {
        // Implement logic to check for a royal straight flush
        // Return true if a royal straight flush is found, false otherwise

        // Check for a straight flush first
        bool hasStraightFlush = CheckForStraightFlush(cardNumbers, cardSuitCount);

        if (!hasStraightFlush)
        {
            return false; // Cannot have a royal straight flush without a straight flush
        }

        // Check for a royal straight flush
        return cardNumbers.SequenceEqual(new List<CardScriptableObject.CardHierarchy>
    {
        CardScriptableObject.CardHierarchy.ten,
        CardScriptableObject.CardHierarchy.Jack,
        CardScriptableObject.CardHierarchy.Queen,
        CardScriptableObject.CardHierarchy.King,
        CardScriptableObject.CardHierarchy.Ace
    });
    }

    private void SavePlayerScore(int score)
    {

    }
    public void UpdateScores(string serverId, Dictionary<string, int> scores)
    {
    }
}
    


