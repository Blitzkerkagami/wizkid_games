using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI; // Added for LayoutRebuilder

public class DropSlot : MonoBehaviour
{
    public TextMeshProUGUI slotText;

    private DraggableLetter currentLetter;
    private AlphabetOrderManager gameManager;

    private void Awake()
    {
        if (slotText == null)
            slotText = GetComponentInChildren<TextMeshProUGUI>();

        gameManager = FindObjectOfType<AlphabetOrderManager>();
    }

    public void PlaceLetter(DraggableLetter letter)
    {
        if (letter == null || gameManager == null) return;

        string expected = gameObject.name;

        if (letter.letterText.text != expected)
        {
            gameManager.ResetIdleTimer(false);

            gameManager.ShowTimedFeedback("Wrong! Try again!", Color.red);
            gameManager.audioSource.PlayOneShot(gameManager.wrongSound);

            letter.transform.SetParent(gameManager.letterPoolParent);

            RectTransform rect = letter.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localPosition = Vector3.zero;
                rect.anchoredPosition = Vector2.zero;
            }
            letter.transform.localScale = Vector3.one;

            LayoutRebuilder.ForceRebuildLayoutImmediate(gameManager.letterPoolParent.GetComponent<RectTransform>());
            return;
        }

        // --- CORRECT ANSWER CHECK ---
        // Grab the letter's image component
        Image letterImage = letter.GetComponent<Image>();

        // Turn off the blinking system BEFORE parenting changes if it matches the current hint
        gameManager.RemoveHintHighlight(true);
        gameManager.ResetIdleTimer(true);

        if (currentLetter != null)
        {
            currentLetter.transform.SetParent(gameManager.letterPoolParent);

            RectTransform currentRect = currentLetter.GetComponent<RectTransform>();
            if (currentRect != null)
            {
                currentRect.localPosition = Vector3.zero;
                currentRect.anchoredPosition = Vector2.zero;
            }
            currentLetter.transform.localScale = Vector3.one;

            LayoutRebuilder.ForceRebuildLayoutImmediate(gameManager.letterPoolParent.GetComponent<RectTransform>());
        }

        currentLetter = letter;
        if (slotText != null)
            slotText.text = letter.letterText.text;

        letter.transform.position = transform.position;
        letter.transform.SetParent(transform);

        RectTransform letterRect = letter.GetComponent<RectTransform>();
        if (letterRect != null)
        {
            letterRect.anchorMin = Vector2.zero;
            letterRect.anchorMax = Vector2.one;
            letterRect.offsetMin = Vector2.zero;
            letterRect.offsetMax = Vector2.zero;
        }
        letter.transform.localScale = Vector3.one;

        gameManager.ShowTimedFeedback("Very Good!", Color.green);
        gameManager.audioSource.PlayOneShot(gameManager.correctSound);

        gameManager.CheckOrder();
    }

    public string GetLetter()
    {
        return slotText != null ? slotText.text : "";
    }

    public DraggableLetter RemoveLetter()
    {
        DraggableLetter letter = currentLetter;
        currentLetter = null;

        if (slotText != null)
            slotText.text = "";

        return letter;
    }

    public void ClearSlot()
    {
        if (slotText != null)
            slotText.text = "";

        currentLetter = null;
    }
}
