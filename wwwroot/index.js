document.addEventListener('htmx:afterSwap', function (event) {
    if (event.detail.target.id === 'addFormInput') {
        let addFormInput = document.getElementById('addFormInput');
        if (addFormInput.lastElementChild.classList.contains('alert-warning')) {
            setTimeout(function() {
                addFormInput.removeChild(addFormInput.lastChild);
            }, 3000);
        } else {
            setTimeout(function() {
                document.getElementById('addForm').reset();
                addFormInput.removeChild(addFormInput.lastChild);
            }, 1000);
        }
    }
});

document.addEventListener('htmx:afterSwap', function (event) {
    if (event.detail.target.id === 'removeFormInput') {
        setTimeout(function() {
            document.getElementById('removeForm').reset();
            document.getElementById('removeFormInput').removeChild(document.getElementById('removeFormInput').lastChild);
        }, 1000);
    }
});