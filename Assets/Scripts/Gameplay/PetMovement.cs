using System.Data.Common;
using GamblingSeriousGame.UI;
using UnityEngine;

public class PetMovement : MonoBehaviour
{
    private AnimalDefinition selectedAnimal;
    private GameStateManager gameStateManager;
    void Start()
    {
        selectedAnimal = gameStateManager.SelectedAnimal;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
