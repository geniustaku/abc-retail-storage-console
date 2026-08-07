// ABC Retail - Azure Storage Web Application
// Author: Genius Mhirizhonga
// Module: CLDV7112 - Cloud Development B

// Shows the chosen file before it is submitted, so the upload is confirmed in the
// browser rather than only after the blob has been written to Azure.
(function () {
    var input = document.getElementById('image');
    var preview = document.getElementById('preview');

    if (!input || !preview) {
        return;
    }

    input.addEventListener('change', function () {
        var file = input.files && input.files[0];
        if (!file) {
            return;
        }

        var reader = new FileReader();
        reader.onload = function (event) {
            preview.innerHTML = '';
            var image = document.createElement('img');
            image.src = event.target.result;
            image.alt = file.name;
            preview.appendChild(image);
        };
        reader.readAsDataURL(file);
    });
})();
