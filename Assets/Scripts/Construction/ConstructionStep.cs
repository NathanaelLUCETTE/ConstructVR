using UnityEngine;

[System.Serializable]
public class ConstructionStep
{
    public string stepName;

    [Header("Pièce à utiliser")]
    public GameObject requiredPiece;

    [Header("Position attendue")]
    public Transform targetPosition;

    [Header("Tolérances")]
    public float positionTolerance = 0.03f;
    public float rotationTolerance = 10f;
}