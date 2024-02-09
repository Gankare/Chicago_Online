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
using Unity.VisualScripting;

public class GameController : MonoBehaviour
{
    public List<GameObject> selectedCardObjects = new();
    public List<Transform> cardSlots = new();
    public List<CardScriptableObject> cards = new();
    public List<CardScriptableObject> deck = new();
    public List<CardScriptableObject> discardPile = new();
    public List<CardScriptableObject> hand = new();
    public List<string> firebaseDeck = new();
    public List<string> firebaseDiscardPile = new();
    public List<string> firebaseHand = new();
    private string serverId;
    private int currentGameRound = 0; // Tracks the current round of the game
    private int playerIndex = 0; // Index of the current player
    private List<string> playerIds = new(); // List of player IDs
    private float turnDuration = 20f; // Time duration for each player's turn
    private float turnTimer = 0f; // Timer for the player's turn

    private void Awake()
    {
        serverId = ServerManager.instance.serverId;
    }

    #region StartGame
    private void Start()
    {
        StartCoroutine(SetStartDeck());
        StartCoroutine(InitializeGameData());
    }
    IEnumerator SetStartDeck()
    {
        deck = cards;
        var setServerDeck = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("cardDeck").SetValueAsync(deck);
        var setServerDiscardPile = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("discardPile").SetValueAsync(firebaseDiscardPile);
        var setUserHand = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").SetValueAsync(firebaseHand);
        yield return new WaitUntil(() => setServerDeck.IsCompleted && setServerDiscardPile.IsCompleted && setUserHand.IsCompleted);
    }

    private IEnumerator InitializeGameData()
    {
        // Get the list of player IDs and initialize their round counters in the database
        var getPlayerIdsTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => getPlayerIdsTask.IsCompleted);

        // Check if the task was successful
        if (getPlayerIdsTask.IsCompleted)
        {
            DataSnapshot snapshot = getPlayerIdsTask.Result;
            if (snapshot.Exists)
            {
                foreach (var childSnapshot in snapshot.Children)
                {
                    string id = childSnapshot.Key;
                    playerIds.Add(id);
                    // Initialize round counter for each player
                    var setRoundCounter = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(id).Child("userGameData").Child("roundCounter").SetValueAsync(0);
                    var setpPlayerHandNull = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(id).Child("userGameData").Child("hand").SetValueAsync("");
                    var setTurnFalse = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(id).Child("userGameData").Child("isTurn").SetValueAsync(false);
                    var setUserHandValue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("handValue").SetValueAsync(0);
                    yield return new WaitUntil(() => setRoundCounter.IsCompleted && setpPlayerHandNull.IsCompleted && setTurnFalse.IsCompleted && setUserHandValue.IsCompleted);
                }

                // Start the game once all player IDs are fetched
                StartGame();
            }
        }
    }

    private void StartGame()
    {
        // Start the game only if there are more than 1 player
        if (playerIds.Count > 1)
        {         
            StartCoroutine(StartNextRound());
        }
        else
        {
            Debug.LogWarning("No players found. Cannot start the game.");
            SceneManager.LoadScene("ServerScene");
        }
    }
    #endregion

    #region StartRoundAndGiveUserCards
    IEnumerator StartNextRound()
    {
        // Start the turn for the current player
        string currentPlayerId = playerIds[playerIndex];
        if (currentPlayerId == DataSaver.instance.userId)
        {
            var setTurnTrue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(true);
            yield return new WaitUntil(() => setTurnTrue.IsCompleted);
            StartCoroutine(UpdateLocalDataFromFirebase());
            StartCoroutine(ShuffleAndDealOwnCards(deck));
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
                StartCoroutine(EndPlayerTurn());
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
            StartCoroutine(UpdateFirebase());
            var setUserRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("userGameData").Child("roundCounter").SetValueAsync(currentGameRound);
            var removeUserTurn = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(false);
            yield return new WaitUntil(() => setUserRound.IsCompleted && removeUserTurn.IsCompleted);
        }

        if (currentPlayerId == DataSaver.instance.userId && (playerIndex + 1) % playerIds.Count == 0)
        {
            var setUserRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("round").SetValueAsync(DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("round").GetValueAsync() + 1.ToString());
            yield return new WaitUntil(() => setUserRound.IsCompleted);
        }
         
        // Move to the next player
        playerIndex = (playerIndex + 1) % playerIds.Count;

        // Start the next player's turn
        StartCoroutine(StartNextRound());
    }

    private IEnumerator ShuffleAndDealOwnCards(List<CardScriptableObject> deck)
    {
        // Shuffle the deck locally
        List<CardScriptableObject> shuffledDeck = ShuffleDeck(deck);

        // Deal cards to the local player
        DealCards(shuffledDeck);
        DisplayCardsDrawn();
        yield return null;
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
            hand.Add(drawnCard);
            // Remove the card from the deck
            shuffledDeck.RemoveAt(0);
        }
    }
    private void DisplayCardsDrawn()
    {
        foreach(CardScriptableObject card in hand)
        {
            var currentCard = Instantiate(card, transform);
            //Position for the card make a horizontal list ting in ui unity later
            currentCard.GetComponent<SpriteRenderer>().sprite = card.cardSprite;
            currentCard.GetComponent<CardInfo>().power = card.power;
            currentCard.GetComponent<CardInfo>().cardId = card.cardId;
        }
    }
    #endregion

    #region UpdateFireBaseAndLocalCards
    IEnumerator UpdateFirebase()
    {
        firebaseHand.Clear();
        foreach (CardScriptableObject card in hand)
            firebaseHand.Add(card.cardId);

        firebaseDiscardPile.Clear();
        foreach (CardScriptableObject card in discardPile)
            firebaseDiscardPile.Add(card.cardId);

        firebaseDeck.Clear();
        foreach (CardScriptableObject card in deck)
            firebaseDeck.Add(card.cardId);

        var setUserHand = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").SetValueAsync(firebaseHand);
        var setServerDiscardPile = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("discardPile").SetValueAsync(firebaseDiscardPile);
        var setServerDeck = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("cardDeck").SetValueAsync(firebaseDeck);
        yield return new WaitUntil(() => setServerDeck.IsCompleted && setServerDiscardPile.IsCompleted && setUserHand.IsCompleted);
    }
    private IEnumerator UpdateLocalDataFromFirebase()
    {
        // Clear the local lists
        deck.Clear();
        hand.Clear();
        discardPile.Clear();

        // Retrieve card IDs from Firebase and convert them back to CardScriptableObject instances
        var getServerDeckTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("cardDeck").GetValueAsync();
        var getServerDiscardPileTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("discardPile").GetValueAsync();
        var getUserHandTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").GetValueAsync();

        // Wait until all data retrieval tasks are completed
        yield return new WaitUntil(() => getServerDeckTask.IsCompleted && getServerDiscardPileTask.IsCompleted && getUserHandTask.IsCompleted);

        if (getServerDeckTask.Exception != null || getServerDiscardPileTask.Exception != null || getUserHandTask.Exception != null)
        {
            Debug.LogError("Error retrieving data from Firebase.");
            yield break; //may have to remove this if it thinks that its a error that it returns a null
        }

        DataSnapshot userHandSnapshot = getUserHandTask.Result;
        DataSnapshot serverDiscardPileSnapshot = getServerDiscardPileTask.Result;
        DataSnapshot serverDeckSnapshot = getServerDeckTask.Result;

        if (userHandSnapshot != null)
        {
            foreach (DataSnapshot cardSnapshot in userHandSnapshot.Children)
            {
                string cardId = cardSnapshot.Value.ToString();
                CardScriptableObject card = GetCardFromId(cardId);
                if (card != null)
                {
                    hand.Add(card);
                }
            }
        }
        if (serverDiscardPileSnapshot != null)
        {
            foreach (DataSnapshot cardSnapshot in serverDiscardPileSnapshot.Children)
            {
                string cardId = cardSnapshot.Value.ToString();
                CardScriptableObject card = GetCardFromId(cardId);
                if (card != null)
                {
                    discardPile.Add(card);
                }
            }
        }
        if (serverDeckSnapshot != null)
        {
            foreach (DataSnapshot cardSnapshot in serverDeckSnapshot.Children)
            {
                string cardId = cardSnapshot.Value.ToString();
                CardScriptableObject card = GetCardFromId(cardId);
                if (card != null)
                {
                    deck.Add(card);
                }
            }
        }
    }

    private CardScriptableObject GetCardFromId(string cardId)
    {
        // Iterate through all card objects to find the one with the matching cardId
        foreach (CardScriptableObject card in cards)
        {
            if (card.cardId == cardId)
            {
                return card;
            }
        }
        Debug.LogError("Card with ID " + cardId + " not found.");
        return null;
    }
    #endregion

    #region PlayerCardActions
    public void SelectCardToThrow(GameObject cardObject)
    {
        // If the card is already selected, deselect it
        if (selectedCardObjects.Contains(cardObject))
        {
            selectedCardObjects.Remove(cardObject);
            cardObject.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else // If the card is not selected, select it
        {
            selectedCardObjects.Add(cardObject);
            cardObject.GetComponent<SpriteRenderer>().color = Color.red;
        }
    }
    public void ThrowCards()
    {
        // Remove all selected cards from the hand list and destroy their corresponding GameObjects
        foreach (GameObject selectedCardObject in selectedCardObjects)
        {
            CardInfo cardInfo = selectedCardObject.GetComponent<CardInfo>();
            hand.Remove(GetCardFromId(cardInfo.cardId));
            Destroy(selectedCardObject);
        }

        // Clear the list of selected card objects
        selectedCardObjects.Clear();
    }
    #endregion

    #region CountValueOfHand
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
    #endregion
}