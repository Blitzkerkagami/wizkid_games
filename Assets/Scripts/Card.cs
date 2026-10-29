using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    [Header("UI Elements")]
    public Image cardImage;
    public Sprite backSprite;
    public Sprite frontSprite;

    private Button cardButton;
    private CardController controller;
    private bool isFlipped = false;
    private bool isMatched = false;

    public int CardID { get; private set; }

    private void Awake()
    {
        cardButton = GetComponent<Button>();
        if (cardButton != null)
            cardButton.onClick.AddListener(OnCardClicked);
    }

    public void SetupCard(int id, Sprite front, Sprite back, CardController gameController)
    {
        CardID = id;
        frontSprite = front;
        backSprite = back;
        controller = gameController;

        HideCard();
    }

    private void OnCardClicked()
    {
        if (isFlipped || isMatched || controller == null || controller.IsCheckingMatches()) return;

        ShowCard();
        controller.OnCardSelected(this);
    }

    public void ShowCard()
    {
        isFlipped = true;
        if (cardImage != null && frontSprite != null)
            cardImage.sprite = frontSprite;
    }

    public void HideCard()
    {
        isFlipped = false;
        if (cardImage != null && backSprite != null)
            cardImage.sprite = backSprite;
    }

    public void SetMatched()
    {
        isMatched = true;
        if (cardButton != null)
            cardButton.interactable = false;
    }
}