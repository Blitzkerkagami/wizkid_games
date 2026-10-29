using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("Name Input")]
    public TMP_InputField nameInput;

    [Header("Age Auto-Detection Field")]
    public TMP_InputField ageInputField;
    private int selectedAge = 5;
    private int selectedBirthDay = 1;

    [Header("Birthday Dropdowns")]
    public TMP_Dropdown monthDropdown;
    public TMP_Dropdown dayDropdown;
    public TMP_Dropdown yearDropdown;

    [Header("Character Selection")]
    public Button[] characterButtons;
    private int selectedCharacter = 0;

    [Header("Let's Play Button")]
    public Button letsPlayButton;

    void Start()
    {
        SetupCharacterButtons();
        SetupBirthdayDropdowns();

        if (monthDropdown != null)
            monthDropdown.onValueChanged.AddListener((val) => UpdateAutoAge());
        if (dayDropdown != null)
            dayDropdown.onValueChanged.AddListener((val) => UpdateAutoAge());
        if (yearDropdown != null)
            yearDropdown.onValueChanged.AddListener((val) => UpdateAutoAge());

        if (letsPlayButton != null)
            letsPlayButton.onClick.AddListener(OnLetsPlayClicked);

        UpdateAutoAge();
    }

    private void SetupCharacterButtons()
    {
        if (characterButtons == null || characterButtons.Length == 0)
            return;

        for (int i = 0; i < characterButtons.Length; i++)
        {
            if (characterButtons[i] == null)
                continue;

            int index = i;
            characterButtons[i].onClick.AddListener(() => SelectCharacter(index));
        }
        SelectCharacter(0);
    }

    private void SelectCharacter(int index)
    {
        selectedCharacter = index;
        for (int i = 0; i < characterButtons.Length; i++)
            if (characterButtons[i] != null)
                characterButtons[i].transform.localScale = (i == index) ? Vector3.one * 1.15f : Vector3.one;
    }

    private void SetupBirthdayDropdowns()
    {
        if (monthDropdown == null || dayDropdown == null || yearDropdown == null)
            return;

        monthDropdown.ClearOptions();
        string[] months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        foreach (string m in months) monthDropdown.options.Add(new TMP_Dropdown.OptionData(m));

        dayDropdown.ClearOptions();
        for (int d = 1; d <= 31; d++) dayDropdown.options.Add(new TMP_Dropdown.OptionData(d.ToString()));

        yearDropdown.ClearOptions();
        int currentYear = DateTime.Today.Year;
        for (int y = 2000; y <= currentYear; y++) yearDropdown.options.Add(new TMP_Dropdown.OptionData(y.ToString()));

        monthDropdown.value = 0;
        dayDropdown.value = 0;
        int defaultBirthYear = currentYear - 6;
        yearDropdown.value = yearDropdown.options.FindIndex(option => option.text == defaultBirthYear.ToString());
        if (yearDropdown.value == -1) yearDropdown.value = 0;
    }

    private void UpdateAutoAge()
    {
        if (monthDropdown == null || dayDropdown == null || yearDropdown == null || ageInputField == null) return;

        int birthMonth = monthDropdown.value + 1;
        int birthDay = dayDropdown.value + 1;

        if (!int.TryParse(yearDropdown.options[yearDropdown.value].text, out int birthYear)) return;

        // Safely clamp days for months like February
        int maxDays = DateTime.DaysInMonth(birthYear, birthMonth);
        if (birthDay > maxDays) birthDay = maxDays;
        selectedBirthDay = birthDay;

        DateTime birthDate = new DateTime(birthYear, birthMonth, birthDay);
        DateTime today = DateTime.Today; // Compares perfectly with current system year (2026)

        int calculatedAge = today.Year - birthDate.Year;

        // Subtract a year if the milestone birthday date hasn't passed yet this year
        if (birthDate.Date > today.AddYears(-calculatedAge))
        {
            calculatedAge--;
        }

        // Keep it safe from negative values, but allow older ages for testing!
        if (calculatedAge < 0) calculatedAge = 0;
        selectedAge = calculatedAge;

        // 1. Assign the value to the text box
        ageInputField.text = selectedAge.ToString();

        // 2. FIXED: Manually toggle the placeholder text component off so it doesn't block the numbers!
        if (ageInputField.placeholder != null)
        {
            ageInputField.placeholder.gameObject.SetActive(string.IsNullOrEmpty(ageInputField.text));
        }

        // 3. Force the UI rendering canvas layer to visually refresh the characters
        ageInputField.ForceLabelUpdate();
    }

    public void OnLetsPlayClicked()
    {
        if (monthDropdown == null || yearDropdown == null)
        {
            Debug.LogError("Birthday dropdowns are not assigned.");
            return;
        }

        string enteredName = nameInput != null ? nameInput.text : "";
        string playerName = string.IsNullOrWhiteSpace(enteredName) ? "Little Adventurer" : enteredName.Trim();

        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetInt("PlayerAge", selectedAge);
        PlayerPrefs.SetString("PlayerBirthday", $"{monthDropdown.options[monthDropdown.value].text} {selectedBirthDay}, {yearDropdown.options[yearDropdown.value].text}");
        PlayerPrefs.SetInt("SelectedWizard", selectedCharacter);
        PlayerPrefs.Save();

        // Keep the offline learner profile current before gameplay starts.
        LocalDatabase.UpsertCurrentLearnerProfile();

        MusicManager.instance?.PlayBackButtonSound();
        SceneManager.LoadScene("AdventureMap");
    }
}
