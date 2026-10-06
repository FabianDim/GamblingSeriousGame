using UnityEngine;
using GamblingSeriousGame.UI;
using System.Collections.Generic;

public class AppNavigationManager : MonoBehaviour
{
    [SerializeField] private GameObject petSelectionScreen;
    [SerializeField] private GameObject petCreationScreen;
    [SerializeField] private GameObject GamePlayScreen;

    List<GameObject> mostRecentPages = new List<GameObject>();

    public string SelectedPetId { get; private set; }

    public void ConfirmPetSelection(string petId)
    {
        if (petSelectionScreen == null || petCreationScreen == null)
        {
            Debug.LogError("Assign both screens on AppNavigationManager.", this);
            return;
        }

        mostRecentPages.Add(petSelectionScreen);

        SelectedPetId = petId;
        Debug.Log($"Pet ID is set as: {petId}");
        petCreationScreen.GetComponent<PetCustomizationScreen>()?.SetPet(petId);
        petSelectionScreen.SetActive(false);
        petCreationScreen.SetActive(true);
    }

    public void ConfirmPetCustomisation()
    {
        if (GamePlayScreen == null || petCreationScreen == null)
        {
            Debug.LogError("Assign the gameplay screen.", this);
            return;
        }

        GamePlayScreen.SetActive(true);
        petCreationScreen.SetActive(false);
    }

    public void BackButton()
    {
        if (mostRecentPages.Count == 0) return;
        GameObject previousPage = mostRecentPages[mostRecentPages.Count - 1];
        mostRecentPages.RemoveAt(mostRecentPages.Count - 1);
        petCreationScreen?.SetActive(false);
        previousPage.SetActive(true);
    }
}
