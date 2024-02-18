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
using static CardScriptableObject;
using System;

public class GameController : MonoBehaviour
{
    public GameObject card;
    public enum Gamestate
    {
        distributionOfCards,
        gambit
    }
    public int currentGameState;
    public List<CardScriptableObject> allCards = new();
    public List<CardScriptableObject> deck = new();
    public List<CardScriptableObject> discardPile = new();
    public List<CardScriptableObject> hand = new();
    public List<string> firebaseDiscardPile = new();
    public List<string> firebaseDeck = new();
    public List<string> firebaseHand = new();
    public List<string> playerIds = new(); // List of player IDs
    public List<TMP_Text> playerScores = new();
    public List<GameObject> selectedCardObjects = new();
    public Transform handSlot;
    public TMP_Text turnTimerText;
    public GameObject endTurnButton;
    private float turnTimer = 0f; // Timer for the player's turn
    private float turnDuration = 20f; // Time duration for each player's turn
    private string serverId;
    private int playerIndex = 0; // Index of the current player
    private bool roundIsActive = false;
    private bool turnEndedEarly = false;
    private DatabaseReference turnTimerRef;

    private void Awake()
    {
        serverId = ServerManager.instance.serverId;
    }

    #region StartGame
    private void Start()
    {
        StartCoroutine(SetStartDeck());
        ListenForTurnTimerChanges();
        StartCoroutine(InitializeGameData());
        StartCoroutine(UpdateFirebase());
    }

    private void ListenForPlayerTurn()
    {
        // Get the reference to the player's isTurn value in the database
        DatabaseReference isTurnRef = DataSaver.instance.dbRef
            .Child("servers")
            .Child(serverId)
            .Child("players")
            .Child(DataSaver.instance.userId)
            .Child("userGameData")
            .Child("isTurn");

        // Add a listener for changes in the isTurn value
        isTurnRef.ValueChanged += PlayerTurnValueChanged;
    }

    private void PlayerTurnValueChanged(object sender, ValueChangedEventArgs args)
    {
        // Check if the value changed and if it became true
        if (args != null && args.Snapshot != null && args.Snapshot.Value != null)
        {
            bool isTurn = (bool)args.Snapshot.Value;
            if (isTurn)
            {
                Debug.Log("Start Turn");
                // Start the next round when a player's turn becomes true
                StartCoroutine(StartNextRound());
            }
        }
    }
    private void ListenForTurnTimerChanges()
    {
        turnTimerRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("turnTimer");
        // Listen for changes in the turn timer value
        turnTimerRef.ValueChanged += TurnTimerValueChanged;
    }
    private void TurnTimerValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args != null && args.Snapshot != null && args.Snapshot.Value != null)
        {
            // Update the local turn timer based on the database value
            if (float.TryParse(args.Snapshot.Value.ToString(), out float newTurnTimer))
            {
                turnTimer = newTurnTimer;
                int remainingSeconds = Mathf.RoundToInt(turnTimer);
                if (remainingSeconds > 0)
                    turnTimerText.text = remainingSeconds.ToString();
                else
                    turnTimerText.text = "";
            }
            else
            {
                Debug.LogError("Failed to parse turn timer value.");
            }
        }
    }

    IEnumerator SetStartDeck()
    {
        currentGameState = (int)Gamestate.distributionOfCards;
        deck = allCards.ToList(); // Create a copy of allCards
        firebaseDeck = deck.Select(card => card.cardId).ToList();
        var setServerDeck = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("cardDeck").SetValueAsync(firebaseDeck);
        var setServerDiscardPile = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("discardPile").SetValueAsync(firebaseDiscardPile);
        var setUserHand = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").SetValueAsync(firebaseHand);
        var setGameRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGameRound").SetValueAsync(0);
        var setScoreRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("scoreGameRound").SetValueAsync(0);
        var setServerTimer = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("turnTimer").SetValueAsync(0);
        yield return new WaitUntil(() => setServerDeck.IsCompleted && setServerDiscardPile.IsCompleted && setUserHand.IsCompleted && setGameRound.IsCompleted && setScoreRound.IsCompleted && setServerTimer.IsCompleted);
    }
   
    private void OnDisable()
    {
        RemoveListeners();
    }

    private void OnDestroy()
    {
        RemoveListeners();
    }
    void RemoveListeners()
    {
        turnTimerRef.ValueChanged -= TurnTimerValueChanged;
        DatabaseReference isTurnRef = DataSaver.instance.dbRef
            .Child("servers")
            .Child(serverId)
            .Child("players")
            .Child(DataSaver.instance.userId)
            .Child("userGameData")
            .Child("isTurn");

        // Add a listener for changes in the isTurn value
        isTurnRef.ValueChanged -= PlayerTurnValueChanged;
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
                }
                    var setPlayerHandNull = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").SetValueAsync("");
                    var setTurnFalse = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("isTurn").SetValueAsync(false);
                    var setPlayerScore = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("score").SetValueAsync(0);
                    var setUserHandValue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("handValue").SetValueAsync(0);
                    yield return new WaitUntil(() => setPlayerHandNull.IsCompleted && setTurnFalse.IsCompleted && setUserHandValue.IsCompleted && setPlayerScore.IsCompleted);

                yield return new WaitForSeconds(1);
                string currentPlayerId = playerIds[playerIndex];
                if (currentPlayerId == DataSaver.instance.userId)
                {
                    var setTurnTrue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(true);
                    yield return new WaitUntil(() => setTurnTrue.IsCompleted);
                    Debug.Log("Setting player turn true");
                }
                ListenForPlayerTurn();
            }
        }
    }
    #endregion

    #region StartRoundAndGiveUserCards
    IEnumerator StartNextRound()
    {
        if (roundIsActive)
        {
            Debug.LogWarning("Round already active.");
            yield break;
        }

        if (playerIds.Count == 0)
        {
            Debug.LogWarning("No player IDs found.");
            yield break;
        }

        if (playerIndex >= playerIds.Count)
        {
            playerIndex = 0;
        }

        bool continueRound = true;
        IsMyTurn((isMyTurn) =>
        {
            if (!isMyTurn)
                continueRound = false;
        });

        yield return StartCoroutine(UpdateLocalDataFromFirebase()); // Wait for data update from Firebase
        yield return new WaitForSeconds(1);
        if (!continueRound)
        {
            Debug.LogWarning("Not my turn");
            yield break;
        }

        roundIsActive = true;
        playerIndex = playerIds.IndexOf(DataSaver.instance.userId);
        Debug.Log("Starting turn for player: " + playerIndex);
        string currentPlayerId = playerIds[playerIndex];

        if (currentGameState == (int)Gamestate.distributionOfCards)
        {
            #region GiveCards
            if (currentPlayerId == DataSaver.instance.userId)
            {
                StartCoroutine(ShuffleAndDealOwnCards(deck));
                endTurnButton.SetActive(true);
            }
        
            if (currentPlayerId == DataSaver.instance.userId)
            {
                var getGameData = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").GetValueAsync();
                yield return new WaitUntil(() => getGameData.IsCompleted);

                // Check if getGameData.Result is not null
                if (getGameData.Result != null)
                {
                    // Retrieve the value of currentGameRound from the DataSnapshot
                    var currentGameRoundSnapshot = getGameData.Result.Child("currentGameRound");
                    if (currentGameRoundSnapshot != null)
                    {
                        string currentValue = currentGameRoundSnapshot.Value.ToString();

                        // Increment the current value
                        int newValue = int.Parse(currentValue) + 1;

                        // Convert the new value to a string
                        string newValueAsString = newValue.ToString();

                        // Set the new string value back to the database
                        var setGameRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGameRound").SetValueAsync(newValueAsString);
                        yield return new WaitUntil(() => setGameRound.IsCompleted);
                    }
                    else
                    {
                        Debug.LogError("currentGameRound is null in the DataSnapshot.");
                    }
                }
                else
                {
                    Debug.LogError("getGameData.Result is null.");
                }
            }
            #endregion
        }
        else if (currentGameState == (int)Gamestate.gambit)
        {
            #region PlayCards

            #endregion
        }
        StartCoroutine(PlayerTurnTimer());
    }

    private IEnumerator PlayerTurnTimer()
    {
        // Set the initial turn timer value
        turnTimer = turnDuration;

        // Loop until the turn timer runs out
        while (turnTimer > 0f)
        {
            // Check if the current player is the one who has the turn
            string currentPlayerId = playerIds[playerIndex];
            if (currentPlayerId == DataSaver.instance.userId)
            {
                turnTimer -= Time.deltaTime;
                // Update the turn timer on the server
                turnTimerRef.SetValueAsync(turnTimer);
            }

            // Check if the turn timer has run out
            if (turnTimer <= 0f)
            {
                if (currentPlayerId == DataSaver.instance.userId && !turnEndedEarly)
                {
                    ThrowCards();
                }
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
        Debug.Log("EndTurn" + currentPlayerId.ToString());
        if (currentPlayerId == DataSaver.instance.userId)
        {
            endTurnButton.SetActive(false);
            DealCards(deck);
            StartCoroutine(DisplayCardsDrawn());
            turnEndedEarly = false;
            StartCoroutine(CountAndSetValueOfHand(hand));
            DatabaseReference gameDataRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData");
            var getGameRoundTask = gameDataRef.Child("currentGameRound").GetValueAsync();
            yield return new WaitUntil(() => getGameRoundTask.IsCompleted);

            if (getGameRoundTask.Result.Exists)
            {
                int currentGameRound = int.Parse(getGameRoundTask.Result.Value.ToString());
                int playerCount = playerIds.Count;

                // Check if the current game round is divisible by the number of players, if all players have 
                if (currentGameRound % playerCount == 0)
                {
                    var setGameRoundTask = gameDataRef.Child("currentGameRound").SetValueAsync(0);
                    var getLastScoreRound = gameDataRef.Child("scoreGameRound").GetValueAsync();
                    yield return new WaitUntil(() => getLastScoreRound.IsCompleted && setGameRoundTask.IsCompleted);

                    string currentValue = getLastScoreRound.Result.Value.ToString();
                    // Increment the current value
                    int newValue = int.Parse(currentValue) + 1;

                    // Convert the new value to a string
                    string newValueAsString = newValue.ToString();

                    // Set the new string value back to the database
                    var setScoreRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("scoreGameRound").SetValueAsync(newValueAsString);
                    yield return new WaitUntil(() => setScoreRound.IsCompleted);
                    yield return StartCoroutine(UpdateFirebase());
                    yield return StartCoroutine(UpdateScore());
                    DatabaseReference gameScoreRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData");
                    var newGetScoreRoundTask = gameDataRef.Child("scoreGameRound").GetValueAsync();
                    yield return new WaitUntil(() => newGetScoreRoundTask.IsCompleted);

                    if (newGetScoreRoundTask.Result.Exists)
                    {
                        int currentScoreRound = int.Parse(getGameRoundTask.Result.Value.ToString());
                        if (currentScoreRound == 3)
                        {
                            currentGameState = (int)Gamestate.gambit;
                            yield break;
                        }
                    }
                }
            }

            if (currentPlayerId == DataSaver.instance.userId)
            {
                yield return StartCoroutine(UpdateFirebase());
                var removeUserTurn = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(false);
                yield return new WaitUntil(() => removeUserTurn.IsCompleted);
            }
            PassTurnToNextPlayer();
            roundIsActive = false;
        }
    }
    IEnumerator IsMyTurnCoroutine(System.Action<bool> callback)
    {
        var isTurnSnapshotTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("isTurn").GetValueAsync();
        yield return new WaitUntil(() => isTurnSnapshotTask.IsCompleted);

        bool isMyTurn = false;
        if (isTurnSnapshotTask.Exception != null)
        {
            Debug.LogError("Error retrieving isTurn value: " + isTurnSnapshotTask.Exception);
        }
        else if (isTurnSnapshotTask.Result != null)
        {
            isMyTurn = (bool)isTurnSnapshotTask.Result.Value;
        }

        Debug.Log("Is it my turn? " + isMyTurn);
        callback?.Invoke(isMyTurn);
    }

    void IsMyTurn(System.Action<bool> callback)
    {
        StartCoroutine(IsMyTurnCoroutine(callback));
    }


    void PassTurnToNextPlayer()
    {
        int currentIndex = playerIds.IndexOf(DataSaver.instance.userId);
        int nextIndex = (currentIndex + 1) % playerIds.Count;
        string nextPlayerId = playerIds[nextIndex];

        // Update the database to indicate it's the next player's turn
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(nextPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(true);
    }

    private IEnumerator ShuffleAndDealOwnCards(List<CardScriptableObject> deckToShuffle)
    {
        // Shuffle the deck locally
        List<CardScriptableObject> shuffledDeck = ShuffleDeck(deckToShuffle);
        deck = shuffledDeck;
        // Deal cards to the local player
        DealCards(deck);
        StartCoroutine(DisplayCardsDrawn());
        yield return null;
    }

    private List<CardScriptableObject> ShuffleDeck(List<CardScriptableObject> deck)
    {
        List<CardScriptableObject> shuffledDeck = new(deck);
        System.Random random = new();
        int n = shuffledDeck.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(0, n + 1);
            CardScriptableObject value = shuffledDeck[k];
            shuffledDeck[k] = shuffledDeck[n];
            shuffledDeck[n] = value;
        }
        return shuffledDeck;
    }

    private void DealCards(List<CardScriptableObject> shuffledDeck)
    {
        int cardsNeeded = 5 - hand.Count; // Calculate how many cards are needed to reach 5
        for (int i = 0; i < cardsNeeded; i++)
        {
            if (shuffledDeck.Count == 0)
            {
                Debug.LogWarning("Deck is empty.");
                //Discardpile into deck fix a function
                return;
            }

            // Draw a card from the top of the deck
            CardScriptableObject drawnCard = shuffledDeck[0];

            // Check if the drawn card is already in the hand
            if (!hand.Contains(drawnCard))
            {
                // Add the card ID to the player's hand
                hand.Add(drawnCard);
            }

            // Remove the card from the deck
            shuffledDeck.RemoveAt(0);
        }
    }

    IEnumerator DisplayCardsDrawn()
    {
        // Iterate through the hand
        foreach (CardScriptableObject slot in hand)
        {
            // Check if the card is already displayed
            bool cardAlreadyDisplayed = false;
            foreach (Transform cardTransform in handSlot)
            {
                CardInfo cardInfo = cardTransform.GetComponent<CardInfo>();
                if (cardInfo != null && cardInfo.cardId == slot.cardId)
                {
                    cardAlreadyDisplayed = true;
                    break;
                }
            }

            // If the card is not already displayed, instantiate it
            if (!cardAlreadyDisplayed)
            {
                var currentCard = Instantiate(card, handSlot);
                currentCard.GetComponent<Image>().sprite = slot.cardSprite;
                currentCard.GetComponent<CardInfo>().power = slot.power;
                currentCard.GetComponent<CardInfo>().cardId = slot.cardId;
                yield return new WaitForSeconds(0.5f);
            }
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
        yield return StartCoroutine(DisplayScore());
    }
    #endregion

    #region PlayerCardActions
    public void SelectCardToThrow(GameObject cardObject)
    {
        // If the card is already selected, deselect it
        if (selectedCardObjects.Contains(cardObject))
        {
            selectedCardObjects.Remove(cardObject);
            cardObject.GetComponent<Image>().color = Color.white;
        }
        else // If the card is not selected, select it
        {
            selectedCardObjects.Add(cardObject);
            cardObject.GetComponent<Image>().color = Color.red;
        }
    }
    public void ThrowCards()
    {
        if (!turnEndedEarly)
        {
            foreach (GameObject selectedCardObject in selectedCardObjects)
            {
                CardInfo cardInfo = selectedCardObject.GetComponent<CardInfo>();
                CardScriptableObject cardToRemove = GetCardFromId(cardInfo.cardId);

                if (cardToRemove != null)
                {
                    hand.Remove(cardToRemove);
                    discardPile.Add(cardToRemove);
                    Destroy(selectedCardObject);
                    Debug.Log("Card thrown: " + cardToRemove.cardId);
                }
                else
                {
                    Debug.LogError("Failed to find card with ID: " + cardInfo.cardId);
                }
            }

            // Clear the list of selected card objects
            selectedCardObjects.Clear();
            turnEndedEarly = true;
            turnTimer = 0;
            turnTimerRef.SetValueAsync(0f);
            StartCoroutine(EndPlayerTurn());
        }
    }

    private CardScriptableObject GetCardFromId(string cardId)
    {
        // Iterate through all card objects to find the one with the matching cardId
        foreach (CardScriptableObject card in allCards)
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

    #region CountValueOfHand
    private IEnumerator CountAndSetValueOfHand(List<CardScriptableObject> playerHand)
    {
        // Calculate the score for the player's hand
        int score = CalculateScore(playerHand);

        // Save the player's hand value to the database
        var setUserHandValue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("handValue").SetValueAsync(score);
        yield return new WaitUntil(() => setUserHandValue.IsCompleted);
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

    private IEnumerator UpdateScore()
    {
        // Retrieve hand values of all players from the database
        Dictionary<string, int> playerHandValues = new();
        Dictionary<string, List<string>> playerHands = new(); // Assuming playerHands holds the cards for each player

        var getPlayersTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => getPlayersTask.IsCompleted);

        if (getPlayersTask.Exception != null)
        {
            Debug.LogError("Error retrieving player data from Firebase.");
            yield break;
        }

        DataSnapshot playersSnapshot = getPlayersTask.Result;

        foreach (var playerSnapshot in playersSnapshot.Children)
        {
            string playerId = playerSnapshot.Key;
            int handValue = int.Parse(playerSnapshot.Child("userGameData").Child("handValue").Value.ToString());
            playerHandValues.Add(playerId, handValue);

            // Assuming the cards are stored as strings in the database
            List<string> cards = new();
            foreach (var cardSnapshot in playerSnapshot.Child("userData").Child("userGameData").Child("hand").Children)
            {
                cards.Add(cardSnapshot.Value.ToString());
            }
            playerHands.Add(playerId, cards);
        }

        // Find the player(s) with the highest hand value
        int highestScore = -1;
        List<string> winningPlayerIds = new();

        foreach (var kvp in playerHandValues)
        {
            if (kvp.Value > highestScore)
            {
                highestScore = kvp.Value;
                winningPlayerIds.Clear();
                winningPlayerIds.Add(kvp.Key);
            }
            else if (kvp.Value == highestScore)
            {
                winningPlayerIds.Add(kvp.Key);
            }
        }

        // If there's only one winning player, update their score directly
        if (winningPlayerIds.Count == 1)
        {
            string winningPlayerId = winningPlayerIds[0];
            int winningScore = highestScore;

            // Add the winning score to the existing score in the database
            var getPlayerScoreTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winningPlayerId).Child("userGameData").Child("score").GetValueAsync();
            yield return new WaitUntil(() => getPlayerScoreTask.IsCompleted);

            if (getPlayerScoreTask.Exception != null)
            {
                Debug.LogError("Error retrieving player score from Firebase.");
                yield break;
            }

            int currentScore = 0;
            if (getPlayerScoreTask.Result.Exists)
            {
                currentScore = int.Parse(getPlayerScoreTask.Result.Value.ToString());
            }

            int updatedScore = currentScore + winningScore;

            var setPlayerScore = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winningPlayerId).Child("userGameData").Child("score").SetValueAsync(updatedScore.ToString());
            yield return new WaitUntil(() => setPlayerScore.IsCompleted);

            if (setPlayerScore.Exception != null)
            {
                Debug.LogError("Error updating player score in Firebase.");
                yield break;
            }
            Debug.Log("Score updated for player: " + winningPlayerId);
            yield return StartCoroutine(DisplayScore());
            yield break;
        }
        else if (winningPlayerIds.Count > 1)
        {
            // If multiple players have the same highest score, compare their hands
            int highestTotalHandValue = 0;
            string winningPlayerId = null;

            foreach (var playerId in winningPlayerIds)
            {
                List<string> cards = playerHands[playerId];
                int totalHandValue = 0;

                foreach (string card in cards)
                {
                    if (Enum.TryParse<CardHierarchy>(card, out CardHierarchy cardValue))
                    {
                        totalHandValue += (int)cardValue; // Sum up the value of each card
                    }
                    else
                    {
                        Debug.LogError("Invalid card value: " + card);
                        yield break;
                    }
                }

                if (totalHandValue > highestTotalHandValue)
                {
                    highestTotalHandValue = totalHandValue;
                    winningPlayerId = playerId;
                }
                else if (totalHandValue == highestTotalHandValue)
                {
                    Debug.LogError("Both players have the same value hand, no one gets score");
                    yield break;
                }
            }

            // Update score for the winning player with the highest total hand value
            if (winningPlayerId != null)
            {
                var getPlayerScoreTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winningPlayerId).Child("userGameData").Child("score").GetValueAsync();
                yield return new WaitUntil(() => getPlayerScoreTask.IsCompleted);

                if (getPlayerScoreTask.Exception != null)
                {
                    Debug.LogError("Error retrieving player score from Firebase.");
                    yield break;
                }

                int currentScore = 0;
                if (getPlayerScoreTask.Result.Exists)
                {
                    currentScore = int.Parse(getPlayerScoreTask.Result.Value.ToString());
                }

                int updatedScore = currentScore + highestTotalHandValue;

                var setPlayerScore = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winningPlayerId).Child("userGameData").Child("score").SetValueAsync(updatedScore.ToString());
                yield return new WaitUntil(() => setPlayerScore.IsCompleted);

                if (setPlayerScore.Exception != null)
                {
                    Debug.LogError("Error updating player score in Firebase.");
                    yield break;
                }
                Debug.Log("Score updated for player: " + winningPlayerId);
                yield return StartCoroutine(DisplayScore());
            }
        }
    }
    public IEnumerator DisplayScore()
    {
        Debug.Log("trying to show score");
        // Retrieve scores of all players from the database
        Dictionary<string, int> playerScoreDictionary = new Dictionary<string, int>();

        var getPlayersTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => getPlayersTask.IsCompleted);

        if (getPlayersTask.IsFaulted)
        {
            Debug.LogError("Error retrieving player data from Firebase: " + getPlayersTask.Exception);
            yield break;
        }

        DataSnapshot playersSnapshot = getPlayersTask.Result;

        foreach (var playerSnapshot in playersSnapshot.Children)
        {
            string playerId = playerSnapshot.Key;
            int score = 0; // Default score to 0

            // If the player has a score value, retrieve it
            if (playerSnapshot.Child("userGameData").Child("score").Exists)
            {
                score = int.Parse(playerSnapshot.Child("userGameData").Child("score").Value.ToString());
            }

            playerScoreDictionary.Add(playerId, score);
        }

        int currentPlayer = 0;
        // Fetch usernames for player IDs
        foreach (var kvp in playerScoreDictionary)
        {
            string playerId = kvp.Key;
            int score = kvp.Value;

            // Fetch username for the player ID
            var getUsernameTask = DataSaver.instance.dbRef.Child("users").Child(playerId).Child("userName").GetValueAsync();
            yield return new WaitUntil(() => getUsernameTask.IsCompleted);

            if (getUsernameTask.IsFaulted)
            {
                Debug.LogError("Error retrieving username for player " + playerId + ": " + getUsernameTask.Exception);
                yield break;
            }

            string username = getUsernameTask.Result.Value.ToString();
            playerScores[currentPlayer].text = $"{username}: {score}";
            currentPlayer++;
        }
    }

    #endregion

    #region Gambit
    IEnumerator Playout()
    {
        //Utspelet sen i slutet s�tta sakerna nedan s� det b�rjas en ny runda.
        string currentPlayerId = playerIds[playerIndex];
        var removeUserTurn = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(false);
        yield return new WaitUntil(() => removeUserTurn.IsCompleted);
        playerIndex = (playerIndex + 1) % playerIds.Count;
        roundIsActive = false;
    }

    #endregion
}