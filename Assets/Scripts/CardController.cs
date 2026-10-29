using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CardController : MonoBehaviour
{
    [Header("Grid & Prefabs")]
    public Transform gridContainer;
    public GameObject cardPrefab;
    public Sprite cardBackSprite;
    public Sprite[] cardFrontSprites;

    [Header("UI Displays")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI feedbackText;

    private List<Card> spawnedCards = new List<Card>();
    private Card firstSelected = null;
    private Card secondSelected = null;

    private int matchesFound = 0;
    private int currentScore = 0;
    private bool isChecking = false;

    void Start()
    {
        GenerateGrid();
        UpdateScoreUI();
    }

    private void GenerateGrid()
    {
        // 1. Clear old elements
        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        spawnedCards.Clear();

        // 2. Build ID pairs based on available sprites
        List<int> cardIDs = new List<int>();
        int pairsCount = cardFrontSprites.Length;

        for (int i = 0; i < pairsCount; i++)
        {
            cardIDs.Add(i);
            cardIDs.Add(i); // Add duplicate for pair
        }

        // Shuffle card IDs
        for (int i = 0; i < cardIDs.Count; i++)
        {
            int temp = cardIDs[i];
            int randomIndex = Random.Range(i, cardIDs.Count);
            cardIDs[i] = cardIDs[randomIndex];
            cardIDs[randomIndex] = temp;
        }

        // 3. Instantiate Cards
        foreach (int id in cardIDs)
        {
            GameObject newCardObj = Instantiate(cardPrefab, gridContainer);
            Card cardComp = newCardObj.GetComponent<Card>();

            if (cardComp != null)
            {
                cardComp.SetupCard(id, cardFrontSprites[id], cardBackSprite, this);
                spawnedCards.Add(cardComp);
            }
        }
    }

    public bool IsCheckingMatches() => isChecking;

    public void OnCardSelected(Card selectedCard)
    {
        if (firstSelected == null)
        {
            firstSelected = selectedCard;
        }
        else if (secondSelected == null && selectedCard != firstSelected)
        {
            secondSelected = selectedCard;
            StartCoroutine(CheckMatchRoutine());
        }
    }

    private IEnumerator CheckMatchRoutine()
    {
        isChecking = true;

        yield return new WaitForSeconds(0.6f);

        if (firstSelected.CardID == secondSelected.CardID)
        {
            // Match found!
            firstSelected.SetMatched();
            secondSelected.SetMatched();

            matchesFound++;
            currentScore += 10;
            UpdateScoreUI();

            if (feedbackText != null)
                feedbackText.text = "Match Found!";

            if (matchesFound >= cardFrontSprites.Length)
            {
                OnGameComplete();
            }
        }
        else
        {
            // No match
            firstSelected.HideCard();
            secondSelected.HideCard();

            if (feedbackText != null)
                feedbackText.text = "Try Again!";
        }

        firstSelected = null;
        secondSelected = null;
        isChecking = false;
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + currentScore;
    }

    private void OnGameComplete()
    {
        if (feedbackText != null)
            feedbackText.text = "You Win!";

        // Save score to local storage
        PlayerPrefs.SetInt("MemoryGame_Score", currentScore);
        PlayerPrefs.Save();

        Invoke("ReturnToMap", 3.0f);
    }

    public void ReturnToMap()
    {
        SceneManager.LoadScene("AdventureMap");
    }
}