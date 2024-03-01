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
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using AYellowpaper.SerializedCollections;


public class GameController : MonoBehaviour
{
    public GameObject card;
    public enum Gamestate
    {
        distributionOfCards,
        gambit
    }

    public ChatManager chatManager;
    public int currentGameState;
    public List<CardScriptableObject> allCards = new();
    public List<CardScriptableObject> deck = new();
    public List<CardScriptableObject> discardPile = new();
    public List<CardScriptableObject> hand = new();
    public List<GameObject> userHandObjects = new();
    public SerializedDictionary<string,CardScriptableObject> gambitCardsInPlay = new();
    public List<CardScriptableObject> gambitCardsToDisplay = new();
    public List<string> gambitCardsToDisplayPlayerIds = new();
    public CardScriptableObject gambitCard;
    public List<string> firebaseDiscardPile = new();
    public List<string> firebaseDeck = new();
    public List<string> firebaseHand = new();
    public string firebaseGambitCard;
    public List<string> playerIds = new();
    public List<string> playerIdsForSlot = new();
    public List<TMP_Text> playerScores = new();
    public List<GameObject> selectedCardObjects = new();
    public Transform handSlot;
    public TMP_Text turnTimerText;
    public GameObject endTurnButton;
    public GameObject winScreen;
    public GameObject loseScreen;
    public List<Transform> gambitSlots = new();
    public Sprite backOfCardSprite;
    public string gambitSuit;
    public Image background;
    public AudioSource cardAudioSource;
    private float turnTimer = 0f; 
    private float turnDuration = 20f; 
    private string serverId;
    private int playerIndex = 0; 
    private bool turnEndedEarly = false;
    private DatabaseReference turnTimerRef;
    private Regex suitRegex = new (@"(Hearts|Diamonds|Clubs|Spades)");
    private Regex numberRegex = new (@"(two|three|four|five|six|seven|eight|nine|ten|Jack|Queen|King|Ace)");
    private bool gameOver;
    private bool firstGambitCard;

    private void Awake()
    {
        serverId = ServerManager.instance.serverId;
    }

    #region StartGame
    private void Start()
    {
        StartCoroutine(SetStartDeck());
    }

    private void ListenForGambitCards()
    {
        foreach (string player in playerIds)
        {
            DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(player).Child("userGameData").Child("gambitCard").ValueChanged += (sender, args) =>
            {
                HandleGambitCardChanged(player, args);
            };
        }
    }
    private void ListenForPlayerTurn()
    {
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("isTurn").ValueChanged += PlayerTurnValueChanged;
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gameOver").ValueChanged += CheckGameOverStatus;
    }
    void RemoveListeners()
    {
        turnTimerRef.ValueChanged -= TurnTimerValueChanged;
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("isTurn").ValueChanged -= PlayerTurnValueChanged;
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gameOver").ValueChanged -= CheckGameOverStatus;
        foreach (string player in playerIds)
        {
            DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(player).Child("userGameData").Child("gambitCard").ValueChanged -= (sender, args) =>
            {
                HandleGambitCardChanged(player, args);
            };
        }
    }

    async void HandleGambitCardChanged(string playerId, ValueChangedEventArgs args)
    {
        if (currentGameState != (int)Gamestate.gambit)
            return;

        var gambitCardId = args.Snapshot.Value.ToString();

        if (!string.IsNullOrEmpty(gambitCardId))
        {
            CardScriptableObject card = GetCardFromId(gambitCardId);
            gambitCardsToDisplay.Add(card);
            gambitCardsToDisplayPlayerIds.Add(playerId);
            if (gambitCardsToDisplay.Count > 0)
            {
                var getGabitSuitSnapshot = await DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gambitSuit").GetValueAsync();
                string gambitSuitValue = getGabitSuitSnapshot.Value.ToString();
                if (!string.IsNullOrEmpty(gambitSuitValue))
                    gambitSuit = gambitSuitValue;
                StartCoroutine(DisplayGambitCards());
            }
            else
                Debug.Log("no gambitcards to display");
        }
    }

    private void PlayerTurnValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args != null && args.Snapshot != null && args.Snapshot.Value != null)
        {
            bool isTurn = (bool)args.Snapshot.Value;
            if (isTurn)
            {
                StartCoroutine(StartNextRoundAfterAWhile());
            }
        }
    }
    IEnumerator StartNextRoundAfterAWhile()
    {
        if (gambitCardsToDisplay != null && currentGameState == (int)Gamestate.distributionOfCards)
            yield return StartCoroutine(ClearGambitDisplayCards());
        if (gambitCardsToDisplayPlayerIds != null && currentGameState == (int)Gamestate.distributionOfCards)
            yield return StartCoroutine(ClearGambitDisplayIds());
            yield return new WaitForSeconds(2);

        StartCoroutine(StartNextRound());
    }
    private void CheckGameOverStatus(object sender, ValueChangedEventArgs args)
    {
        if (args != null && args.Snapshot != null && args.Snapshot.Value != null)
        {
            bool isGameOver = (bool)args.Snapshot.Value;
            if (isGameOver)
            {
                StartCoroutine(GameOver());
            }
        }
    }
    private void ListenForTurnTimerChanges()
    {
        turnTimerRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("turnTimer");
        turnTimerRef.ValueChanged += TurnTimerValueChanged;
    }
    private void TurnTimerValueChanged(object sender, ValueChangedEventArgs args)
    {
        if (args != null && args.Snapshot != null && args.Snapshot.Value != null)
        {
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
    private void OnDisable()
    {
        RemoveListeners();
    }

    private void OnDestroy()
    {
        RemoveListeners();
    }
    IEnumerator SetStartDeck()
    {
        currentGameState = (int)Gamestate.distributionOfCards;
        deck = allCards.ToList();
        firebaseDeck = deck.Select(card => card.cardId).ToList();
        var setServerDeck = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("cardDeck").SetValueAsync(firebaseDeck);
        var setServerDiscardPile = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("discardPile").SetValueAsync(firebaseDiscardPile);
        var setUserHand = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").SetValueAsync(firebaseHand);
        var setGameRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGameRound").SetValueAsync(0);
        var setGambiteRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGambitRound").SetValueAsync(0);
        var setScoreRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("scoreGameRound").SetValueAsync(0);
        var setServerTimer = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("turnTimer").SetValueAsync(0);
        var setGameOverFalse = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gameOver").SetValueAsync(false);
        var setGambitFalse = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gambit").SetValueAsync(false);
        var setGabitSuitFalse = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gambitSuit").SetValueAsync("");
        yield return new WaitUntil(() => setGabitSuitFalse.IsCompleted && setServerDeck.IsCompleted && setServerDiscardPile.IsCompleted && setUserHand.IsCompleted && setGameRound.IsCompleted
        && setScoreRound.IsCompleted && setGambiteRound.IsCompleted && setServerTimer.IsCompleted && setGameOverFalse.IsCompleted && setGambitFalse.IsCompleted);
        StartCoroutine(InitializeGameData());
    }
  
    private IEnumerator InitializeGameData()
    {
        var getPlayerIdsTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => getPlayerIdsTask.IsCompleted);

        if (getPlayerIdsTask.IsCompleted)
        {
            DataSnapshot snapshot = getPlayerIdsTask.Result;    
            if (snapshot.Exists)
            {
                playerIdsForSlot.Add(DataSaver.instance.userId);
                foreach (var childSnapshot in snapshot.Children)
                {
                    string id = childSnapshot.Key;
                    playerIds.Add(id);
                    if (id != DataSaver.instance.userId)
                        playerIdsForSlot.Add(id);
                }
                    var setPlayerGambitCardNull = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("gambitCard").SetValueAsync("");
                    var setTurnFalse = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("isTurn").SetValueAsync(false);
                    var setPlayerScore = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("score").SetValueAsync(0);
                    var setUserHandValue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("handValue").SetValueAsync(0);
                    yield return new WaitUntil(() => setPlayerGambitCardNull.IsCompleted && setTurnFalse.IsCompleted && setUserHandValue.IsCompleted && setPlayerScore.IsCompleted);

                string currentPlayerId = playerIds[playerIndex];
                if (currentPlayerId == DataSaver.instance.userId)
                {
                    chatManager.AddMessageToChat($"{playerIds.Count} Players connected");
                    var setTurnTrue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(true);
                    yield return new WaitUntil(() => setTurnTrue.IsCompleted);
                }
                yield return StartCoroutine(UpdateFirebase());
                yield return StartCoroutine(DisplayScore());
                ListenForGambitCards();
                ListenForTurnTimerChanges();
                ListenForPlayerTurn();
            }
        }
    }
    #endregion

    #region StartRoundAndGiveUserCards
    IEnumerator StartNextRound()
    {
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

        if (!continueRound)
        {
            Debug.LogWarning("Not my turn");
            yield break;
        }
        yield return StartCoroutine(UpdateLocalDataFromFirebase());
        yield return StartCoroutine(DisplayScore());

        playerIndex = playerIds.IndexOf(DataSaver.instance.userId);
        string currentPlayerId = playerIds[playerIndex];

        if (currentGameState == (int)Gamestate.distributionOfCards) 
        {
            yield return StartCoroutine(ShuffleAndDealOwnCards(deck));
        
            var getCurrentGameRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGameRound").GetValueAsync();
            yield return new WaitUntil(() => getCurrentGameRound.IsCompleted);
            if (getCurrentGameRound.Result != null)
            {
                int newGameRoundValue = int.Parse(getCurrentGameRound.Result.Value.ToString()) + 1;
                var setGameRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGameRound").SetValueAsync(newGameRoundValue);
                yield return new WaitUntil(() => setGameRound.IsCompleted);
            }
            else
            {
                Debug.LogError("Can't get currentGameRound");
            }
        }
        else if (currentGameState == (int)Gamestate.gambit)
        {
            yield return StartCoroutine(UpdateCardButtons());

            var getCurrentGameRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGambitRound").GetValueAsync();
            yield return new WaitUntil(() => getCurrentGameRound.IsCompleted);
            if (getCurrentGameRound.Result != null)
            {
                int newGambitRoundValue = int.Parse(getCurrentGameRound.Result.Value.ToString()) + 1;
                var setGambitRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGambitRound").SetValueAsync(newGambitRoundValue);
                yield return new WaitUntil(() => setGambitRound.IsCompleted);
            }
            else
            {
                Debug.LogError("Can't get currentGambitRound");
            }
        }
        endTurnButton.SetActive(true);
        StartCoroutine(PlayerTurnTimer());
    }

    private IEnumerator PlayerTurnTimer()
    {
        turnTimer = turnDuration;
        float startTime = Time.time;
        int previousTurnTimer = Mathf.RoundToInt(turnTimer);

        if (currentGameState == (int)Gamestate.distributionOfCards)
        {
            while (Time.time - startTime <= turnDuration && !turnEndedEarly)
            {
                turnTimer = turnDuration - (Time.time - startTime);
                int currentTurnTimer = Mathf.RoundToInt(turnTimer); 
                if (currentTurnTimer != previousTurnTimer) 
                {
                    turnTimerRef.SetValueAsync(currentTurnTimer); 
                    previousTurnTimer = currentTurnTimer; 
                }
                yield return null;
            }

            if (!turnEndedEarly)
            {
                ThrowCards();
                yield break;
            }
        }
        else if (currentGameState == (int)Gamestate.gambit)
        {
            while (Time.time - startTime <= turnDuration && !turnEndedEarly)
            {
                turnTimer = turnDuration - (Time.time - startTime);
                int currentTurnTimer = Mathf.RoundToInt(turnTimer); 
                if (currentTurnTimer != previousTurnTimer) 
                {
                    turnTimerRef.SetValueAsync(currentTurnTimer); 
                    previousTurnTimer = currentTurnTimer; 
                }
                yield return null;
            }

            if (!turnEndedEarly && selectedCardObjects.Count > 0)
            {
                ThrowCards();
                yield break;
            }
            else if (!turnEndedEarly && selectedCardObjects.Count == 0)
            {
                if (!string.IsNullOrEmpty(gambitSuit)) // Check if there is a suit/color to follow
                {
                    int amountOfSuitInHand = 0;
                    List<GameObject> cardsOfSuitInHand = new();
                    foreach (GameObject card in userHandObjects)
                    {
                        if (card.gameObject.GetComponent<CardInfo>().cardId.Contains(gambitSuit))
                        {
                            amountOfSuitInHand++;
                            cardsOfSuitInHand.Add(card);
                        }
                    }

                    if (amountOfSuitInHand > 0) // If suit and have same color in hand, autoselect first card of that color
                    {
                        cardsOfSuitInHand[0].GetComponent<CardButtonClick>().SelectCard();
                        ThrowCards();
                        yield break;
                    }
                    else if (amountOfSuitInHand == 0) // If suit and no suit card in hand, autoselect the first card in hand
                    {
                        userHandObjects[0].GetComponent<CardButtonClick>().SelectCard();
                        ThrowCards();
                        yield break;
                    }
                }
                else // If no suit/color, autoselect the first card in hand
                {
                    userHandObjects[0].GetComponent<CardButtonClick>().SelectCard();
                    ThrowCards();
                    yield break;
                }
            }
        }
    }

    IEnumerator EndPlayerTurn()
    {
        string currentPlayerId = playerIds[playerIndex];
        if (currentGameState == (int)Gamestate.distributionOfCards) 
        {
            endTurnButton.SetActive(false);
            DealCards(deck);
            yield return StartCoroutine(DisplayCardsDrawn());
            turnEndedEarly = false;
            StartCoroutine(CountAndSetValueOfHand(hand));

            DatabaseReference gameDataRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData");
            var getGameRoundTask = gameDataRef.Child("currentGameRound").GetValueAsync();
            yield return new WaitUntil(() => getGameRoundTask.IsCompleted);

            if (getGameRoundTask.Result.Exists)
            {
                int currentGameRound = int.Parse(getGameRoundTask.Result.Value.ToString());
                int playerCount = playerIds.Count;

                if (currentGameRound % playerCount == 0)
                {
                    var setGameRoundTask = gameDataRef.Child("currentGameRound").SetValueAsync(0);
                    var getLastScoreRound = gameDataRef.Child("scoreGameRound").GetValueAsync();
                    yield return new WaitUntil(() => getLastScoreRound.IsCompleted && setGameRoundTask.IsCompleted);

                    int newScoreRoundValue = int.Parse(getLastScoreRound.Result.Value.ToString()) + 1;

                    var setScoreRound = gameDataRef.Child("scoreGameRound").SetValueAsync(newScoreRoundValue);
                    yield return new WaitUntil(() => setScoreRound.IsCompleted);
                    yield return StartCoroutine(UpdateFirebase());
                    yield return StartCoroutine(UpdateScore());
                    
                    if (newScoreRoundValue == 3)
                    {
                        currentGameState = (int)Gamestate.gambit;
                        var setGambitTrue = gameDataRef.Child("gambit").SetValueAsync(true);
                        var setScoreRoundZero = gameDataRef.Child("scoreGameRound").SetValueAsync(0);
                        yield return new WaitUntil(() => setGambitTrue.IsCompleted && setScoreRoundZero.IsCompleted);
                    }
                }
            }
        }
        else if (currentGameState == (int)Gamestate.gambit)
        {
            bool gambitOver = false;
            turnEndedEarly = false;
            endTurnButton.SetActive(false);

            foreach (GameObject card in userHandObjects)
            {
                card.GetComponent<Button>().enabled = true;
                card.GetComponent<Image>().color = Color.white;
            }
            DatabaseReference gameDataRef = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData");
            var getGambitRoundTask = gameDataRef.Child("currentGambitRound").GetValueAsync();
            yield return new WaitUntil(() => getGambitRoundTask.IsCompleted);

            if (getGambitRoundTask.Result.Exists)
            {
                int currentGambitRound = int.Parse(getGambitRoundTask.Result.Value.ToString());
                int playerCount = playerIds.Count;

                if (currentGambitRound % playerCount == 0)
                {
                    var setGambitRoundTask = gameDataRef.Child("currentGambitRound").SetValueAsync(0);
                    yield return new WaitUntil(() => setGambitRoundTask.IsCompleted);
                    string winnerId = "";
                    int higestCard = 0;

                    foreach (var gambitCard in gambitCardsInPlay)
                    {
                        if (gambitCard.Value.cardId.Contains(gambitSuit))
                        {
                            if (((int)gambitCard.Value.cardNumber) > higestCard)
                            {
                                higestCard = (int)gambitCard.Value.cardNumber;
                                winnerId = gambitCard.Key;
                            }
                        }
                    }
                    if (hand.Count == 0 || hand == null)
                    {
                        gambitOver = true;
                        var getPlayerScoreTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winnerId).Child("userGameData").Child("score").GetValueAsync();
                        yield return new WaitUntil(() => getPlayerScoreTask.IsCompleted);

                        if (getPlayerScoreTask.Exception != null)
                        {
                            Debug.LogError("Error retrieving player score from Firebase.");
                            yield break;
                        }
                        int currentScore = int.Parse(getPlayerScoreTask.Result.Value.ToString());

                        int updatedScore = currentScore + 5;

                        var setPlayerScore = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winnerId).Child("userGameData").Child("score").SetValueAsync(updatedScore);
                        yield return new WaitUntil(() => setPlayerScore.IsCompleted);

                        string username;
                        DatabaseReference userRef = DataSaver.instance.dbRef.Child("users").Child(winnerId);
                        var fetchTask = userRef.Child("userName").GetValueAsync();
                        yield return new WaitUntil(() => fetchTask.IsCompleted);
                        username = fetchTask.Result.Value.ToString();
                        chatManager.AddScoreMessageToChat($"{5} points ", username, 5, true);

                        StartCoroutine(DisplayScore());
                        if (updatedScore >= 52) //Player wins 
                        {
                            var setGameOverTrue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gameOver").SetValueAsync(true);
                            yield return new WaitUntil(() => setGameOverTrue.IsCompleted);
                            yield break;
                        }
                        //deleting all cards in the scene and clearing all lists before updating to firebase
                        yield return StartCoroutine(UpdateDeckAsync());
                        var setGambitFalse = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gambit").SetValueAsync(false);
                        var setGambitSuit = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gambitSuit").SetValueAsync("");
                        yield return new WaitUntil(() => setGambitFalse.IsCompleted && setGambitSuit.IsCompleted);
                    }
                    //Clear all players gambitcards in the database
                    yield return StartCoroutine(UpdateFirebase());
                    foreach (string playerid in playerIds)
                    {
                        var setGambitCards = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(playerid).Child("userGameData").Child("gambitCard").SetValueAsync("");
                        yield return new WaitUntil(() => setGambitCards.IsCompleted);
                    }
                    gambitCard = null;
                    var removeUserTurn = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(false);
                    yield return new WaitUntil(() => removeUserTurn.IsCompleted);
                    if (!gameOver)
                    {
                        if(gambitOver) 
                            yield return new WaitForSeconds(3f);
                        else
                            yield return new WaitForSeconds(1f);
                        PassTurnToGambitWinner(winnerId);
                    }
                    yield break;   
                }
            }
        }
        if (!gameOver)
        {
            yield return StartCoroutine(UpdateFirebase());
            var removeUserTurn = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(currentPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(false);
            yield return new WaitUntil(() => removeUserTurn.IsCompleted);
            PassTurnToNextPlayer(); 
        }
        else
            yield break;
    }
    IEnumerator UpdateDeckAsync()
    {
        discardPile.Clear();
        deck.Clear();
        foreach (var card in allCards)
        {
            deck.Add(card);
            yield return null; 
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
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(nextPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(true);
    }
    void PassTurnToGambitWinner(string winnerId)
    {
        int nextIndex = playerIds.IndexOf(winnerId);
        string nextPlayerId = playerIds[nextIndex];
        DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(nextPlayerId).Child("userGameData").Child("isTurn").SetValueAsync(true);
    }

    private IEnumerator ShuffleAndDealOwnCards(List<CardScriptableObject> deckToShuffle)
    {
        List<CardScriptableObject> shuffledDeck = ShuffleDeck(deckToShuffle);
        deck = shuffledDeck;

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
        int cardsNeeded = 5 - hand.Count;
        if (cardsNeeded > 0)
        {
            for (int i = 0; i < cardsNeeded; i++)
            {
                if (shuffledDeck.Count == 0)
                {
                    Debug.LogWarning("Deck is empty.");
                    StartCoroutine(DeckEmptyReShuffle());
                    return;
                }
                CardScriptableObject drawnCard = shuffledDeck[0];

                if (!hand.Contains(drawnCard))
                {
                    hand.Add(drawnCard);
                }
                shuffledDeck.RemoveAt(0);
            }
        }
    }
    IEnumerator DeckEmptyReShuffle()
    {
        foreach (CardScriptableObject card in discardPile)
        {
            deck.Add(card);
        }
        discardPile.Clear();
        yield return StartCoroutine(ShuffleAndDealOwnCards(deck));
    }

    IEnumerator DisplayCardsDrawn()
    {
        foreach (CardScriptableObject slot in hand)
        {
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
            if (!cardAlreadyDisplayed)
            {
                var currentCard = Instantiate(card, handSlot);
                currentCard.GetComponent<Image>().sprite = slot.cardSprite;
                currentCard.GetComponent<CardInfo>().power = slot.power;
                currentCard.GetComponent<CardInfo>().cardId = slot.cardId;
                userHandObjects.Add(currentCard);
                cardAudioSource.PlayOneShot(cardAudioSource.clip);
                yield return new WaitForSeconds(0.5f);
            }
        }
        yield return null;
    }

    IEnumerator DisplayGambitCards()
    {
        for (int i = 0; i < playerIdsForSlot.Count; i++)
        {
            // Proceed with displaying cards if the lists are not null or empty
            if (gambitCardsToDisplay == null || gambitCardsToDisplayPlayerIds == null ||
            gambitCardsToDisplay.Count == 0 || gambitCardsToDisplayPlayerIds.Count == 0)
            {
                Debug.LogWarning("Gambit cards to display list is null or empty.");
                yield break; 
            }
            string playerId = playerIdsForSlot[i];
            float xOffset = -0.5f;

            // Iterate through the cards for the current player
            for (int j = 0; j < gambitCardsToDisplayPlayerIds.Count; j++)
            {
                string cardPlayerId = gambitCardsToDisplayPlayerIds[j];
                CardScriptableObject cardToDisplay = gambitCardsToDisplay[j];

                // Check if the card is for the current player
                if (cardPlayerId == playerId)
                {
                    xOffset += 0.5f;
                    // Check if the card is already displayed
                    bool cardAlreadyDisplayed = false;
                    foreach (Transform existingCardTransform in gambitSlots[i])
                    {
                        CardInfo existingCardInfo = existingCardTransform.GetComponent<CardInfo>();
                        if (existingCardInfo != null && existingCardInfo.cardId == cardToDisplay.cardId)
                        {
                            cardAlreadyDisplayed = true;
                            break;
                        }
                    }

                    // If the card is not already displayed, instantiate it
                    if (!cardAlreadyDisplayed)
                    {
                        var currentCard = Instantiate(card, gambitSlots[i]);
                        if (cardToDisplay.cardId.Contains(gambitSuit) || gambitSuit == string.Empty)
                            currentCard.GetComponent<Image>().sprite = cardToDisplay.cardSprite;
                        else
                            currentCard.GetComponent<Image>().sprite = backOfCardSprite;
                        currentCard.GetComponent<CardInfo>().power = cardToDisplay.power;
                        currentCard.GetComponent<CardInfo>().cardId = cardToDisplay.cardId;
                        currentCard.GetComponent<Button>().enabled = false;
                        currentCard.transform.position = new Vector2(currentCard.transform.position.x + xOffset, currentCard.transform.position.y);
                        cardAudioSource.PlayOneShot(cardAudioSource.clip);
                    }
                }
            }
        }
        yield return null;
    }

    #endregion

    #region UpdateFireBaseAndLocalCards
    IEnumerator UpdateFirebase()
    {
        // Clear firebase lists
        firebaseHand.Clear();
        firebaseDiscardPile.Clear();
        firebaseDeck.Clear();

        // Update hand list
        yield return UpdateHandList();

        // Update discard pile list
        yield return UpdateDiscardPileList();

        // Update deck list
        yield return UpdateDeckList();

        // Update gambit card
        UpdateGambitCard();

        // Wait for all setValueAsync operations to complete
        yield return WaitForSetValueAsyncCompletion();
    }

    IEnumerator UpdateHandList()
    {
        if (hand.Count == 0 || hand == null)
        {
            firebaseHand.Add("");
        }
        else
        {
            foreach (CardScriptableObject card in hand)
                firebaseHand.Add(card.cardId);
        }
        yield return null;
    }

    IEnumerator UpdateDiscardPileList()
    {
        if (discardPile.Count == 0 || discardPile == null)
        {
            firebaseDiscardPile.Add("");
        }
        else
        {
            foreach (CardScriptableObject card in discardPile)
                firebaseDiscardPile.Add(card.cardId);
        }
        yield return null;
    }

    IEnumerator UpdateDeckList()
    {
        if (deck.Count == 0 || deck == null)
        {
            firebaseDeck.Add("");
        }
        else
        {
            foreach (CardScriptableObject card in deck)
                firebaseDeck.Add(card.cardId);
        }
        yield return null;  
    }

    void UpdateGambitCard()
    {
        if (gambitCard != null)
            firebaseGambitCard = gambitCard.cardId;
        else 
            firebaseGambitCard = "";
    }

    IEnumerator WaitForSetValueAsyncCompletion()
    {
        var setUserHand = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").SetValueAsync(firebaseHand);
        var setServerDiscardPile = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("discardPile").SetValueAsync(firebaseDiscardPile);
        var setServerDeck = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("cardDeck").SetValueAsync(firebaseDeck);
        var setUserGambitCard = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("gambitCard").SetValueAsync(firebaseGambitCard);

        yield return new WaitUntil(() => setUserHand.IsCompleted && setServerDiscardPile.IsCompleted && setServerDeck.IsCompleted && setUserGambitCard.IsCompleted);
    }


    private IEnumerator UpdateLocalDataFromFirebase()
    {
        deck.Clear();
        hand.Clear();
        discardPile.Clear();
        userHandObjects.Clear();

        var getUserHandTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("hand").GetValueAsync();
        var getServerDiscardPileTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("discardPile").GetValueAsync();
        var getServerDeckTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("cardDeck").GetValueAsync();
        var getGambitBool = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gambit").GetValueAsync();
        yield return new WaitUntil(() =>  getGambitBool.IsCompleted && getServerDeckTask.IsCompleted && getServerDiscardPileTask.IsCompleted && getUserHandTask.IsCompleted);

        if (getGambitBool.Exception != null || getServerDeckTask.Exception != null || getServerDiscardPileTask.Exception != null || getUserHandTask.Exception != null)
        {
            Debug.LogError("Error retrieving data from Firebase.");
            yield break;
        }
        DataSnapshot userHandSnapshot = getUserHandTask.Result;
        DataSnapshot serverDiscardPileSnapshot = getServerDiscardPileTask.Result;
        DataSnapshot serverDeckSnapshot = getServerDeckTask.Result;

        yield return PopulateListFromSnapshot(hand, userHandSnapshot);
        yield return PopulateListFromSnapshot(discardPile, serverDiscardPileSnapshot);
        yield return PopulateListFromSnapshot(deck, serverDeckSnapshot);

        if (hand.Count > 0)
        {
            for (int i = 0; i < handSlot.childCount; i++) 
            {
                userHandObjects.Add(handSlot.GetChild(i).gameObject);
            }
        }

        if ((bool)getGambitBool.Result.Value) //If gambit is taking place, Get all cards in play to a list for displaying
        {
            background.color = new Color(1, 0.55f, 0.55f); 
            gambitCardsInPlay.Clear();
            foreach (string playerId in playerIds)
            {
                var gambitCardTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(playerId).Child("userGameData").Child("gambitCard").GetValueAsync();
                yield return new WaitUntil(() => gambitCardTask.IsCompleted);
                if (gambitCardTask.Exception != null)
                {
                    Debug.LogError("Error retrieving gambit card for player " + playerId + ": " + gambitCardTask.Exception.Message);
                    continue;
                }
                if (gambitCardTask.Result.Value.ToString() == string.Empty)
                {
                    Debug.Log("Player has no card out");
                    continue;
                }
                else
                {
                    string cardId = gambitCardTask.Result.Value.ToString();
                    CardScriptableObject card = GetCardFromId(cardId);
                    if (card != null)
                    {
                        gambitCardsInPlay.Add(playerId, card);
                    }
                }
            }
            currentGameState = (int)Gamestate.gambit;
            selectedCardObjects.Clear();
            var getGambitRound = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("currentGambitRound").GetValueAsync();
            yield return new WaitUntil(() => getGambitRound.IsCompleted);
            if (int.Parse(getGambitRound.Result.Value.ToString()) == 0)
            {
                firstGambitCard = true;
                var setGabitSuitFalse = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gambitSuit").SetValueAsync("");
                yield return new WaitUntil(() => setGabitSuitFalse.IsCompleted);
                gambitSuit = "";
            }
            else
            {
                var getGabitSuit = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gambitSuit").GetValueAsync();
                yield return new WaitUntil(() => getGabitSuit.IsCompleted);
                firstGambitCard = false;
                gambitSuit = getGabitSuit.Result.Value.ToString();
            }
        }
        else
        {
            gambitCard = null;
            gambitCardsInPlay.Clear();
            background.color = new Color(0.49f, 0.66f, 0.59f); //Original background color
            for (int i = 0; i < gambitSlots.Count; i++)
            {
                if (gambitSlots[i].childCount > 0)
                {
                    for (int childCard = 0; childCard < gambitSlots[i].childCount; childCard++)
                    {
                        Destroy(gambitSlots[i].GetChild(childCard).gameObject);
                    }
                }
            }
            currentGameState = (int)Gamestate.distributionOfCards;
        }
    }
    IEnumerator PopulateListFromSnapshot(List<CardScriptableObject> list, DataSnapshot snapshot)
    {
        if (snapshot != null)
        {
            foreach (DataSnapshot cardSnapshot in snapshot.Children)
            {
                string cardId = cardSnapshot.Value.ToString();
                CardScriptableObject card = GetCardFromId(cardId); // Here in the start everytime
                if (card != null)
                {
                    list.Add(card);
                }
            }
        }
        yield return null; 
    }
    #endregion

    #region PlayerCardActions
    public void SelectCardToThrow(GameObject cardObject)
    {
        if (currentGameState == (int)Gamestate.distributionOfCards)
        {
            if (selectedCardObjects.Contains(cardObject))
            {
                selectedCardObjects.Remove(cardObject);
                cardObject.GetComponent<Image>().color = Color.white;
            }
            else
            {
                selectedCardObjects.Add(cardObject);
                cardObject.GetComponent<Image>().color = new Color(1, 0.55f, 0.55f);
            }           
        }
        else if (currentGameState == (int)Gamestate.gambit) //If gambit then only 1 card can be choosen
        {
            if (selectedCardObjects.Contains(cardObject))
            {
                selectedCardObjects.Remove(cardObject);
                cardObject.GetComponent<Image>().color = new Color(0.95f, 1, 0.75f);
            }
            else
            {
                foreach (GameObject card in selectedCardObjects) 
                {
                    card.GetComponent<Image>().color = new Color(0.95f, 1, 0.75f);
                }
                selectedCardObjects.Clear();
                selectedCardObjects.Add(cardObject);
                cardObject.GetComponent<Image>().color = new Color(1, 0.55f, 0.55f);
            }
        }
    }
    public void ThrowCards()
    {
        if (!turnEndedEarly && currentGameState == (int)Gamestate.distributionOfCards)
        {
            chatManager.AddDiscardMessageToChat($"Threw {selectedCardObjects.Count} Cards", DataSaver.instance.dts.userName);
            foreach (GameObject selectedCardObject in selectedCardObjects)
            {
                CardInfo cardInfo = selectedCardObject.GetComponent<CardInfo>();
                CardScriptableObject cardToRemove = GetCardFromId(cardInfo.cardId);

                if (cardToRemove != null)
                {
                    userHandObjects.Remove(selectedCardObject);
                    hand.Remove(cardToRemove);
                    discardPile.Add(cardToRemove);
                    Destroy(selectedCardObject);
                }
                else
                {
                    Debug.LogError("Failed to find card with ID: " + cardInfo.cardId);
                }
            }
            selectedCardObjects.Clear();
            turnEndedEarly = true;
            turnTimer = 0;
            turnTimerRef.SetValueAsync(0f); 
            Invoke(nameof(InvokeEndPlayerTurn), 0.5f);
        }
        else if (!turnEndedEarly && currentGameState == (int)Gamestate.gambit)
        {
            if (selectedCardObjects.Count > 0)
            {
                foreach (GameObject selectedCardObject in selectedCardObjects)
                {
                    CardInfo cardInfo = selectedCardObject.GetComponent<CardInfo>();
                    CardScriptableObject cardToRemove = GetCardFromId(cardInfo.cardId);

                    if (cardToRemove != null)
                    {
                        hand.Remove(cardToRemove);
                        userHandObjects.Remove(selectedCardObject);
                        if (firstGambitCard)
                        {
                            DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gambitSuit").SetValueAsync(cardToRemove.cardSuit.ToString());
                            gambitSuit = cardToRemove.cardSuit.ToString();
                        }
                        if (gambitCard != null)
                        {
                            discardPile.Add(gambitCard);
                            gambitCardsInPlay.Remove(DataSaver.instance.userId);
                        }
                        gambitCard = cardToRemove;
                        gambitCardsInPlay.Add(DataSaver.instance.userId, gambitCard);
                        Destroy(selectedCardObject);
                    }
                    else
                    {
                        Debug.LogError("Failed to find card with ID: " + cardInfo.cardId);
                    }
                }
                selectedCardObjects.Clear();
                turnEndedEarly = true;
                turnTimer = 0;
                turnTimerRef.SetValueAsync(0f);
                Invoke(nameof(InvokeEndPlayerTurn), 0.5f);
            }
            else
                Debug.Log("No card selected to play");
        }
    }
    void InvokeEndPlayerTurn()
    {
        StartCoroutine(EndPlayerTurn());
    }
    IEnumerator ClearGambitDisplayCards()
    {
        gambitCardsToDisplay?.Clear();
        yield return null;
    }
    IEnumerator ClearGambitDisplayIds()
    {
        gambitCardsToDisplayPlayerIds?.Clear();
        yield return null;
    }
    private CardScriptableObject GetCardFromId(string cardId)
    {
        // Iterate through all card objects to find the one with the matching cardId
        if (string.IsNullOrEmpty(cardId)) 
        {
            Debug.LogWarning("getcardfromid got an empty cardid string"); 
            return null;
        }
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

    IEnumerator UpdateCardButtons()
    {
        if (currentGameState == (int)Gamestate.distributionOfCards)
        {
            if (hand.Count != 0)
            {
                foreach (GameObject card in userHandObjects)
                {
                    card.GetComponent<Button>().enabled = true;
                    card.GetComponent<Image>().color = Color.white;
                }
            }
            yield return null;
        }

        else if (currentGameState == (int)Gamestate.gambit)
        {
            if (!string.IsNullOrEmpty(gambitSuit))
            {
                int amountOfSuitInHand = 0;
                if(hand.Count != 0)
                {
                    foreach (GameObject card in userHandObjects)
                    {
                        if (!card.GetComponent<CardInfo>().cardId.Contains(gambitSuit))
                        {
                            card.GetComponent<Button>().enabled = false;
                            card.GetComponent<Image>().color = Color.grey;
                        }
                        else
                        {
                            card.GetComponent<Button>().enabled = true;
                            card.GetComponent<Image>().color = new Color(0.95f, 1, 0.75f);
                            amountOfSuitInHand++;
                        }
                    }
                    if (amountOfSuitInHand == 0)
                    {
                        foreach (GameObject card in userHandObjects)
                        {
                            card.GetComponent<Button>().enabled = true;
                            card.GetComponent<Image>().color = new Color(0.95f, 1, 0.75f);
                        }
                    }
                }
            }
            else if (firstGambitCard)
            {
                foreach (GameObject card in userHandObjects)
                {
                    card.GetComponent<Button>().enabled = true;
                    card.GetComponent<Image>().color = new Color(0.95f, 1, 0.75f);
                }
            }
        }
    }
    #endregion

    #region CountValueOfHand
    private IEnumerator CountAndSetValueOfHand(List<CardScriptableObject> playerHand)
    {
        int score = CalculateScore(playerHand);
        var setUserHandValue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("handValue").SetValueAsync(score);
        yield return new WaitUntil(() => setUserHandValue.IsCompleted);
    }
    private int CalculateScore(List<CardScriptableObject> playerCards)
    {
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
        cardNumbers.Sort();

        int consecutiveCount = 1;

        // Check for a straight (five consecutive cards)
        for (int i = 1; i < cardNumbers.Count; i++)
        {
            if (cardNumbers[i] == cardNumbers[i - 1] + 1)
            {
                consecutiveCount++;
                if (consecutiveCount == 5)
                {
                    return true;
                }
            }
            else 
            {
                consecutiveCount = 1;
            }
        }
        return false;
    }


    private bool CheckForStraightFlush(List<CardScriptableObject.CardHierarchy> cardNumbers, Dictionary<CardScriptableObject.Suit, int> cardSuitCount)
    {
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
        Dictionary<string, int> playerHandValues = new();
        Dictionary<string, List<string>> playerHands = new();
        
        var getPlayersTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").GetValueAsync();
        yield return new WaitUntil(() => getPlayersTask.IsCompleted);

        if (getPlayersTask.Exception != null)
        {
            Debug.LogError("Error retrieving player data from Firebase.");
            yield break;
        }

        var playersSnapshot = getPlayersTask.Result;

        foreach (var playerSnapshot in playersSnapshot.Children)
        {
            string playerId = playerSnapshot.Key;
            int handValue = int.Parse(playerSnapshot.Child("userGameData").Child("handValue").Value.ToString());
            playerHandValues.Add(playerId, handValue);

            List<string> cards = new();
            foreach (var cardSnapshot in playerSnapshot.Child("userGameData").Child("hand").Children)
            {
                cards.Add(cardSnapshot.Value.ToString());
            }
            playerHands.Add(playerId, cards);
            Debug.Log($"Player {playerId} hand: {string.Join(", ", cards)}");
        }

        // Find the players with the highest hand value
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
          if (highestScore == 0)
        {
            Debug.Log("No one got points");
            yield break;
        }
        // If there's only one winning player, update their score directly
        if (winningPlayerIds.Count == 1)
        {
            string winningPlayerId = winningPlayerIds[0];
            int winningScore = highestScore;

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

            var setPlayerScore = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winningPlayerId).Child("userGameData").Child("score").SetValueAsync(updatedScore);
            yield return new WaitUntil(() => setPlayerScore.IsCompleted);

            string username;
            DatabaseReference userRef = DataSaver.instance.dbRef.Child("users").Child(winningPlayerId);
            var fetchTask = userRef.Child("userName").GetValueAsync();
            yield return new WaitUntil(() => fetchTask.IsCompleted);
            username = fetchTask.Result.Value.ToString();

            chatManager.AddScoreMessageToChat($"{highestScore} points ", username, highestScore, false);
            if (setPlayerScore.Exception != null)
            {
                Debug.LogError("Error updating player score in Firebase.");
                yield break;
            }
            yield return StartCoroutine(DisplayScore());
            if (updatedScore >= 2) //Player wins 
            {
                var setGameOverTrue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gameOver").SetValueAsync(true);
                yield return new WaitUntil(() => setGameOverTrue.IsCompleted);
            }
            yield break;
        }
        else if (winningPlayerIds.Count > 1)
        {
            // If multiple players have the same highest score, compare their hands
            int highestTotalHandValue = 0;
            string winningPlayerId = null;

            foreach (var playerId in winningPlayerIds)
            {
                List<string> cardsInHighscoreHands = playerHands[playerId];
                int totalHandValue = 0;

                Debug.Log($"Player ID: {playerId}");

                foreach (string card in cardsInHighscoreHands)
                {
                    Match suitMatch = suitRegex.Match(card);
                    Match numberMatch = numberRegex.Match(card);

                    if (suitMatch.Success && numberMatch.Success)
                    {
                        // Extract suit and number from the card string
                        string suitStr = suitMatch.Value;
                        string numberStr = numberMatch.Value;

                        if (Enum.TryParse<CardScriptableObject.Suit>(suitStr, out CardScriptableObject.Suit cardSuit) &&
                            Enum.TryParse<CardScriptableObject.CardHierarchy>(numberStr, out CardScriptableObject.CardHierarchy cardNumber))
                        {
                            int cardValue = (int)cardNumber;
                            totalHandValue += cardValue; // Sum up the value of each card
                            Debug.Log($"Card: {card}, Value: {cardValue}, Total Hand Value: {totalHandValue}");
                        }
                        else
                        {
                            Debug.LogError("Invalid card format: " + card);
                            yield break;
                        }
                    }
                    else
                    {
                        Debug.LogError("Invalid card format: " + card);
                        yield break;
                    }
                }

                Debug.Log($"Total Hand Value for Player ID {playerId}: {totalHandValue}");

                if (totalHandValue > highestTotalHandValue)
                {
                    highestTotalHandValue = totalHandValue;
                    winningPlayerId = playerId;
                }
                else if (totalHandValue == highestTotalHandValue)
                {
                    chatManager.AddMessageToChat("players have the same value hand, no score");
                    Debug.LogError("Both players have the same value hand, no one gets score");
                    yield break;
                }
            }

            // Update score for the winning player with the highest total hand value
            if (winningPlayerId != null)
            {
                Debug.Log(highestTotalHandValue);
                var getPlayerScoreTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winningPlayerId).Child("userGameData").Child("score").GetValueAsync();
                var getPlayerHandValueTask = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winningPlayerId).Child("userGameData").Child("handValue").GetValueAsync();
                yield return new WaitUntil(() => getPlayerScoreTask.IsCompleted && getPlayerHandValueTask.IsCompleted);

                if (getPlayerScoreTask.Exception != null)
                {
                    Debug.LogError("Error retrieving player score from Firebase.");
                    yield break;
                }
                int handValue = int.Parse(getPlayerHandValueTask.Result.Value.ToString());
                int currentScore = 0;
                if (getPlayerScoreTask.Result.Exists)
                {
                    currentScore = int.Parse(getPlayerScoreTask.Result.Value.ToString());
                }

                int updatedScore = currentScore + handValue;

                var setPlayerScore = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(winningPlayerId).Child("userGameData").Child("score").SetValueAsync(updatedScore);
                yield return new WaitUntil(() => setPlayerScore.IsCompleted);

                string username;
                DatabaseReference userRef = DataSaver.instance.dbRef.Child("users").Child(winningPlayerId);
                var fetchTask = userRef.Child("userName").GetValueAsync();
                yield return new WaitUntil(() => fetchTask.IsCompleted);
                username = fetchTask.Result.Value.ToString();

                chatManager.AddScoreMessageToChat($"{highestScore} points ", username, highestScore, false);

                if (setPlayerScore.Exception != null)
                {
                    Debug.LogError("Error updating player score in Firebase.");
                    yield break;
                }
                yield return StartCoroutine(DisplayScore());
                if (updatedScore >= 52) //Player wins 
                {
                    var setGameOverTrue = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("gameData").Child("gameOver").SetValueAsync(true);
                    yield return new WaitUntil(() => setGameOverTrue.IsCompleted);
                }
            }
        }
    }

    public IEnumerator DisplayScore()
    {
        Dictionary<string, int> playerScoreDictionary = new();

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
            int score = 0;

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
            if (playerId == DataSaver.instance.userId)
            {
                playerScores[currentPlayer].color = new Color(0.67f, 1, 0.57f);
            }
            currentPlayer++;
        }
    }
    IEnumerator GameOver()
    {
        gameOver = true;
        var scoreString = DataSaver.instance.dbRef.Child("servers").Child(serverId).Child("players").Child(DataSaver.instance.userId).Child("userGameData").Child("score").GetValueAsync();
        yield return new WaitUntil(() => scoreString.IsCompleted);

        int userScore = int.Parse(scoreString.Result.Value.ToString());
        if (userScore >= 2) //Winner
        {
            winScreen.SetActive(true);
            //Adding a win to winners profile
            var getUserWins = DataSaver.instance.dbRef.Child("users").Child(DataSaver.instance.userId).Child("matchesWon").GetValueAsync();
            yield return new WaitUntil(() => getUserWins.IsCompleted);

            int userWins = int.Parse(getUserWins.Result.Value.ToString());

            int newUserWins = userWins + 1;

            var addToUserWins = DataSaver.instance.dbRef.Child("users").Child(DataSaver.instance.userId).Child("matchesWon").SetValueAsync(newUserWins);
            yield return new WaitUntil(() => addToUserWins.IsCompleted);
            DataSaver.instance.dts.matchesWon++;
            yield return new WaitForSeconds(5f);
            //Deleting server
            var deletingServer = DataSaver.instance.dbRef.Child("servers").Child(serverId).RemoveValueAsync();
            yield return new WaitUntil(() => deletingServer.IsCompleted);
            if (deletingServer.Exception != null)
            {
                Debug.LogError("Error deleting server node: " + deletingServer.Exception);
            }
        }
        else //Loser
        {
            loseScreen.SetActive(true);
            yield return new WaitForSeconds(5);
        }
        Destroy(ServerManager.instance.gameObject);
        SceneManager.LoadScene("ServerScene");
    }
    #endregion
}