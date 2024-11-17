// Get references to the form elements.
const form = document.getElementById('uploadForm');
const resultsDiv = document.getElementById('results');
const successfulReadingsSpan = document.getElementById('successfulReadings');
const failedReadingsSpan = document.getElementById('failedReadings');
const errorSpan = document.getElementById('error');

// Add an event listener to the form for when it's submitted.
form.addEventListener('submit', async (event) => {
    event.preventDefault(); // Prevent the default form submission behavior.

    // Get the selected file from the file input.
    const file = document.getElementById('csvFileInput').files[0];

    // Validate that a file is selected.
    if (!file) {
        showError("Please select a file.");
        return;
    }

    // Create a FormData object to send the file.
    const formData = new FormData();
    formData.append('file', file);

    try {
        // Send a POST request to the server to upload the file.
        const response = await fetch('/meter-reading-uploads', {
            method: 'POST',
            body: formData
        });

        // Check if the request was successful.
        if (response.ok) {
            // Parse the JSON response from the server.
            const data = await response.json();

            // Check if the expected properties are present in the response.
            if (data.successfulReadings !== undefined && data.failedReadings !== undefined) {
                // Update the results display with the counts.
                successfulReadingsSpan.textContent = data.successfulReadings;
                failedReadingsSpan.textContent = data.failedReadings;
                resultsDiv.style.display = 'block'; // Show the results.
                errorSpan.textContent = ''; // Clear any previous errors.
            } else {
                // Handle missing properties in the response.
                showError("Missing properties in response.");
            }
        } else {
            // Handle HTTP errors from the server.
            const errorMessage = `Error: ${response.status} - ${response.statusText}`;
            showError(errorMessage);
        }
    } catch (error) {
        // Handle network errors or other exceptions.
        showError('Error uploading file.');
    }
});

/**
 * Displays an error message to the user.
 * @param {string} message - The error message to display.
 */
function showError(message) {
    console.error(message); // Log the error to the console.
    errorSpan.textContent = message; // Display the error message.
    resultsDiv.style.display = 'none'; // Hide the results.
}