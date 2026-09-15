window.dropdownPositioner = {
    shouldOpenUpwards: (elementId) => {
        const element = document.getElementById(elementId);
        if (!element) return false;

        const rect = element.getBoundingClientRect();
        const spaceBelow = window.innerHeight - rect.bottom;
        const neededSpace = 250; // Matches your CSS max-height

        // If space below is less than needed, return true (Open Upwards)
        return spaceBelow < neededSpace;
    }
};