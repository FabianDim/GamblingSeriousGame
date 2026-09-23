using UnityEngine;

public class AppNavigationManager : MonoBehaviour
{
    [SerializeField] private GameObject petSelectionScreen;
    [SerializeField] private GameObject petCreationScreen;

    public string SelectedPetId { get; private set; }

    public void ConfirmPetSelection(string petId)
    {
        SelectedPetId = petId;

        petSelectionScreen.SetActive(false);
        petCreationScreen.SetActive(true);
    }
}