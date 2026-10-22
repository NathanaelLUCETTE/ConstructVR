using UnityEngine;

public class ConstructionManager : MonoBehaviour
{
    public ConstructionStep[] steps;

    private int currentStep = 0;

    public int CurrentStep => currentStep;

    private float elapsedTime = 0f;
    private int errors = 0;

    private void Update()
    {
        elapsedTime += Time.deltaTime;
    }

    public ConstructionStep GetCurrentStep()
    {
        if (currentStep >= steps.Length)
            return null;

        return steps[currentStep];
    }

    public void ValidatePiece(GameObject piece)
    {
        ConstructionStep step = GetCurrentStep();

        if (step == null)
            return;

        if (piece != step.requiredPiece)
        {
            RegisterError();
            return;
        }

        if (!IsCorrectPosition(piece, step))
        {
            RegisterError();
            return;
        }

        CompleteStep();
    }

    private bool IsCorrectPosition(
        GameObject piece,
        ConstructionStep step)
    {
        float distance = Vector3.Distance(
            piece.transform.position,
            step.targetPosition.position
        );

        float rotationDifference = Quaternion.Angle(
            piece.transform.rotation,
            step.targetPosition.rotation
        );

        return
            distance <= step.positionTolerance &&
            rotationDifference <= step.rotationTolerance;
    }

    private void CompleteStep()
    {
        Debug.Log("Étape réussie !");

        currentStep++;

        if (currentStep >= steps.Length)
        {
            FinishConstruction();
        }
    }

    private void RegisterError()
    {
        errors++;

        Debug.Log("Erreur de montage !");
    }

    private void FinishConstruction()
    {
        Debug.Log("CONSTRUCTION TERMINÉE !");
        Debug.Log("Temps : " + elapsedTime);
        Debug.Log("Erreurs : " + errors);
    }
}