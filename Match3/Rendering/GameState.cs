namespace Match3Easter;

public enum GameState
{
    Idle, // Wait input
    Swapping, // Swap elements
    SwappingBack, // Swap Elements Back
    CreateBonuses,
    // MergeBonuses, // Merge one bonus into another

    // ActivateBonuses, // Activate bonus
    Matching, // Found matching highlighting
    Removing, // Elements disappear
    Falling, // Elements Falls
    Filling // Elements Creates
}