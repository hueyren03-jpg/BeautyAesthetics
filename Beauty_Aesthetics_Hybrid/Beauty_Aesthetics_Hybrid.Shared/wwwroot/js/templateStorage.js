// Template Storage using localStorage
window.templateStorage = {
    // Save custom templates to localStorage
    saveTemplates: function (templatesJson) {
        try {
            localStorage.setItem('customTemplates', templatesJson);
            console.log('Templates saved to localStorage');
            return true;
        } catch (error) {
            console.error('Error saving templates:', error);
            return false;
        }
    },

    // Load custom templates from localStorage
    loadTemplates: function () {
        try {
            const templatesJson = localStorage.getItem('customTemplates');
            console.log('Templates loaded from localStorage');
            return templatesJson || null;
        } catch (error) {
            console.error('Error loading templates:', error);
            return null;
        }
    },

    // Save document types to localStorage
    saveDocumentTypes: function (typesJson) {
        try {
            localStorage.setItem('documentTypes', typesJson);
            console.log('Document types saved to localStorage');
            return true;
        } catch (error) {
            console.error('Error saving document types:', error);
            return false;
        }
    },

    // Load document types from localStorage
    loadDocumentTypes: function () {
        try {
            const typesJson = localStorage.getItem('documentTypes');
            console.log('Document types loaded from localStorage');
            return typesJson || null;
        } catch (error) {
            console.error('Error loading document types:', error);
            return null;
        }
    },

    // Clear all template data
    clearAll: function () {
        try {
            localStorage.removeItem('customTemplates');
            localStorage.removeItem('documentTypes');
            console.log('All template data cleared from localStorage');
            return true;
        } catch (error) {
            console.error('Error clearing template data:', error);
            return false;
        }
    },

    // Get storage info
    getStorageInfo: function () {
        try {
            const templates = localStorage.getItem('customTemplates');
            const types = localStorage.getItem('documentTypes');
            return {
                hasTemplates: templates !== null,
                hasDocumentTypes: types !== null,
                templatesSize: templates ? templates.length : 0,
                typesSize: types ? types.length : 0
            };
        } catch (error) {
            console.error('Error getting storage info:', error);
            return null;
        }
    }
};
