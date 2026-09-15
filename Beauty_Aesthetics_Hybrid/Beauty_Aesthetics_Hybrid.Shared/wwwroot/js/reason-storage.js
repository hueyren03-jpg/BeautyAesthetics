/**
 * ==========================================
 * CUSTOM REASON STORAGE MANAGER
 * ==========================================
 * Handles localStorage operations for custom medical certificate reasons
 */

const STORAGE_KEY = 'medicalCertificateReasons';

// Default reasons to initialize with
const DEFAULT_REASONS = [
    { key: "Upper Respiratory Tract Infection", value: "The patient is diagnosed with an upper respiratory tract infection and requires rest." },
    { key: "Acute Gastroenteritis", value: "The patient is experiencing acute gastroenteritis and needs time to recover." },
    { key: "Fever", value: "The patient is suffering from fever and is advised to rest at home." },
    { key: "Migraine", value: "The patient is experiencing severe migraine and requires time off." },
    { key: "Back Pain", value: "The patient has acute lower back pain and needs rest and physiotherapy." },
    { key: "Medical Follow-up", value: "The patient requires time off for medical follow-up appointments." }
];

/**
 * Initialize storage with default reasons if empty
 */
window.initializeReasonStorage = function() {
    try {
        const existing = localStorage.getItem(STORAGE_KEY);
        if (!existing) {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(DEFAULT_REASONS));
            console.log('✓ Reason storage initialized with defaults');
        }
        return true;
    } catch (error) {
        console.error('Failed to initialize reason storage:', error);
        return false;
    }
};

/**
 * Get all custom reasons
 */
window.getAllReasons = function() {
    try {
        const data = localStorage.getItem(STORAGE_KEY);
        if (!data) {
            window.initializeReasonStorage();
            return DEFAULT_REASONS;
        }
        return JSON.parse(data);
    } catch (error) {
        console.error('Failed to get reasons:', error);
        return DEFAULT_REASONS;
    }
};

/**
 * Get a single reason by key
 */
window.getReasonByKey = function(key) {
    try {
        const reasons = window.getAllReasons();
        return reasons.find(r => r.key === key) || null;
    } catch (error) {
        console.error('Failed to get reason:', error);
        return null;
    }
};

/**
 * Add a new reason
 */
window.addReason = function(key, value) {
    try {
        const reasons = window.getAllReasons();
        
        // Check maximum limit (10 reasons)
        if (reasons.length >= 10) {
            return { success: false, error: 'Maximum limit reached. You can only have 10 reasons.' };
        }
        
        // Check for duplicate key
        if (reasons.some(r => r.key === key)) {
            return { success: false, error: 'A reason with this name already exists' };
        }
        
        reasons.push({ key, value });
        localStorage.setItem(STORAGE_KEY, JSON.stringify(reasons));
        
        console.log(`✓ Added reason: ${key}`);
        return { success: true };
    } catch (error) {
        console.error('Failed to add reason:', error);
        return { success: false, error: error.message };
    }
};

/**
 * Update an existing reason
 */
window.updateReason = function(oldKey, newKey, newValue) {
    try {
        const reasons = window.getAllReasons();
        const index = reasons.findIndex(r => r.key === oldKey);
        
        if (index === -1) {
            return { success: false, error: 'Reason not found' };
        }
        
        // Check if new key conflicts with another reason (unless it's the same)
        if (oldKey !== newKey && reasons.some(r => r.key === newKey)) {
            return { success: false, error: 'A reason with this name already exists' };
        }
        
        reasons[index] = { key: newKey, value: newValue };
        localStorage.setItem(STORAGE_KEY, JSON.stringify(reasons));
        
        console.log(`✓ Updated reason: ${oldKey} → ${newKey}`);
        return { success: true };
    } catch (error) {
        console.error('Failed to update reason:', error);
        return { success: false, error: error.message };
    }
};

/**
 * Delete a reason by key
 */
window.deleteReason = function(key) {
    try {
        const reasons = window.getAllReasons();
        const filtered = reasons.filter(r => r.key !== key);
        
        if (filtered.length === reasons.length) {
            return { success: false, error: 'Reason not found' };
        }
        
        localStorage.setItem(STORAGE_KEY, JSON.stringify(filtered));
        
        console.log(`✓ Deleted reason: ${key}`);
        return { success: true };
    } catch (error) {
        console.error('Failed to delete reason:', error);
        return { success: false, error: error.message };
    }
};

/**
 * Reset to default reasons
 */
window.resetReasonsToDefault = function() {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(DEFAULT_REASONS));
        console.log('✓ Reasons reset to defaults');
        return { success: true };
    } catch (error) {
        console.error('Failed to reset reasons:', error);
        return { success: false, error: error.message };
    }
};

/**
 * Export reasons as JSON file
 */
window.exportReasons = function() {
    try {
        const reasons = window.getAllReasons();
        const dataStr = JSON.stringify(reasons, null, 2);
        const dataBlob = new Blob([dataStr], { type: 'application/json' });
        const url = URL.createObjectURL(dataBlob);
        
        const link = document.createElement('a');
        link.href = url;
        link.download = `mc-reasons-${new Date().toISOString().split('T')[0]}.json`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
        
        console.log('✓ Reasons exported');
        return { success: true };
    } catch (error) {
        console.error('Failed to export reasons:', error);
        return { success: false, error: error.message };
    }
};

/**
 * Import reasons from JSON
 */
window.importReasons = function(jsonString) {
    try {
        const imported = JSON.parse(jsonString);
        
        // Validate structure
        if (!Array.isArray(imported)) {
            return { success: false, error: 'Invalid format: Expected an array' };
        }
        
        for (const item of imported) {
            if (!item.key || !item.value) {
                return { success: false, error: 'Invalid format: Each reason must have "key" and "value"' };
            }
        }
        
        localStorage.setItem(STORAGE_KEY, JSON.stringify(imported));
        
        console.log(`✓ Imported ${imported.length} reasons`);
        return { success: true, count: imported.length };
    } catch (error) {
        console.error('Failed to import reasons:', error);
        return { success: false, error: error.message };
    }
};

// Initialize on load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => window.initializeReasonStorage());
} else {
    window.initializeReasonStorage();
}

