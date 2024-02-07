using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Firebase;
using Firebase.Database;
using UnityEngine.UI;
using TMPro;
using Firebase.Extensions;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public List<CardScriptableObject> cards = new();
    public List<string> deck = new();
    public List<string> discardPile = new();
    public List<string> playersHand = new();
    private string serverId;
    private int currentGameRound = 0; // Tracks the current round of the game
    private List<string> playerIds = new(); // List of player IDs
    private int playerIndex = 0; // Index of the current player
    private float turnDuration = 20f; // Time duration for each player's turn
    private float turnTimer = 0f; // Timer for the player's turn

    private void Awake()
    {
        serverId = ServerManager.instance.serverId;

        // Initialize player's round counter and other necessary data
        InitializeGameData();
    }

    private void InitializeGameData()
    {
        // Get the list of player IDs and initialize their round counters in the database
        var getPlayerIdsTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").GetValueAsync();
        getPlayerIdsTask.ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                DataSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    foreach (var childSnapshot in snapshot.Children)
                    {
                        string id = childSnapshot.Key;
                        playerIds.Add(id);
                        // Initialize round counter for each player
                        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(id).Child("roundCounter").SetValueAsync(0);
                    }

                    // Start the game once all player IDs are fetched
                    StartGame();
                }
            }
        });
    }

    private void StartGame()
    {
        // Start the game only if there are more than 1 player
        if (playerIds.Count > 1)
        {
            // Start the first round
            StartCoroutine(StartNextRound());
        }
        else
        {
            Debug.LogWarning("No players found. Cannot start the game.");
            SceneManager.LoadScene("ServerScene");
        }
    }

    IEnumerator StartNextRound()
    {
        // Start the turn for the current player
        string currentPlayerId = playerIds[playerIndex];
        if (currentPlayerId == DataSaver.instance.userId)
        {
            var setTurn = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("isTurn").SetValueAsync(true);
            yield return new WaitUntil(() => setTurn.IsCompleted);
        }
        // Increment the current game round
        currentGameRound++;
        if (currentPlayerId == DataSaver.instance.userId)
        {
            var setGameRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGameRound").SetValueAsync(currentGameRound);
            yield return new WaitUntil(() => setGameRound.IsCompleted);
        }


        // Start the turn timer
        turnTimer = turnDuration;
        StartCoroutine(PlayerTurnTimer());
    }

    private IEnumerator PlayerTurnTimer()
    {
        // Set the initial turn timer value
        turnTimer = turnDuration;

        // Loop until the turn timer runs out
        while (turnTimer > 0f)
        {
            // Update the turn timer
            turnTimer -= Time.deltaTime;

            // Check if the turn timer has run out
            if (turnTimer <= 0f)
            {
                // End the player's turn if the timer runs out
                EndPlayerTurn();
                yield break; // Exit the coroutine
            }

            yield return null; // Wait for the next frame
        }
    }

    IEnumerator EndPlayerTurn()
    {
        string currentPlayerId = playerIds[playerIndex];

        if (currentPlayerId == DataSaver.instance.userId)
        {
            var setUserRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("roundCounter").SetValueAsync(currentGameRound);
            var removeUserTurn = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("isTurn").SetValueAsync(false);
            yield return new WaitUntil(() => setUserRound.IsCompleted && removeUserTurn.IsCompleted);
        }

        // Move to the next player
        playerIndex = (playerIndex + 1) % playerIds.Count;

        // Start the next player's turn
        StartCoroutine(StartNextRound());
    }
    private void PopulateCardsList()
    {
        foreach(CardScriptableObject card in cards)
        {
            deck.Add(card.cardId);
        }
    }

    private IEnumerator ShuffleAndDealOwnCards(List<CardScriptableObject> deck)
    {
        // Shuffle the deck locally
        List<CardScriptableObject> shuffledDeck = ShuffleDeck(deck);

        // Deal cards to the local player
        DealCards(shuffledDeck);

        // Save deck and discard pile to the database
        var setServerDeck = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("cardDeck").SetValueAsync(shuffledDeck);
        var setServerDiscardPile = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("discardPile").SetValueAsync(discardPile);
        var setUserHand = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").SetValueAsync(playersHand);
        yield return new WaitUntil(() => setServerDeck.IsCompleted && setServerDiscardPile.IsCompleted && setUserHand.IsCompleted);
    }

    private List<CardScriptableObject> ShuffleDeck(List<CardScriptableObject> deck)
    {
        List<CardScriptableObject> shuffledDeck = new(deck);
        int n = shuffledDeck.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            CardScriptableObject value = shuffledDeck[k];
            shuffledDeck[k] = shuffledDeck[n];
            shuffledDeck[n] = value;
        }
        return shuffledDeck;
    }

    private void DealCards(List<CardScriptableObject> shuffledDeck)
    {
        int cardsToDeal = 5; // Adjust as needed
        for (int i = 0; i < cardsToDeal; i++)
        {
            // Draw a card from the top of the deck
            CardScriptableObject drawnCard = shuffledDeck[0];
            // Add the card ID to the player's hand
            playersHand.Add(drawnCard.cardId);
            // Remove the card from the deck
            shuffledDeck.RemoveAt(0);
        }

        // Update UI to display player's hand
        UpdatePlayerHandUI();
    }

    private void UpdatePlayerHandUI()
    {
        // Example code to update the UI goes here
        // You might want to instantiate card prefabs and position them accordingly based on the player's hand
    }



















    // Method to handle card throw event
    public void ThrowCard(string cardId)
    {
        // Remove the card from the player's hand
        playersHand.Remove(cardId);
        // Add the card to the discard pile
        discardPile.Add(cardId);

        // Update UI to reflect the changes
        UpdatePlayerHandUI();
    }

    private IEnumerator CountAndSetValueOfHand()
    {
        // Retrieve the player's cards from the database
        List<CardScriptableObject> playerCards = GetPlayerCards();

        // Calculate the score for the player's hand
        int score = CalculateScore(playerCards);

        // Save the player's hand value to the database
        var setUserHandValue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("handValue").SetValueAsync(score);
        yield return new WaitUntil(() => setUserHandValue.IsCompleted);
    }

    private List<CardScriptableObject> GetPlayerCards()
    {
        // Fetch the player's hand from the database and convert it to CardScriptableObject instances
        List<CardScriptableObject> playerCards = new();
        // Implement the logic to retrieve the player's hand from the database
        return playerCards;
    }

    IEnumerator RoundCounter(int roundConter)
    {
        var setUserRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("round").SetValueAsync(roundConter);
        yield return new WaitUntil(() => setUserRound.IsCompleted);
    }
    private int CalculateScore(List<CardScriptableObject> playerCards)
    {
        // Count occurrences of each card number and suit
        Dictionary<CardScriptableObject.CardHierarchy, int> cardNumberCount = new();
        Dictionary<CardScriptableObject.Suit, int> cardSuitCount = new();

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

}