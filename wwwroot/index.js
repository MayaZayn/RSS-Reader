document.addEventListener('htmx:afterSwap', function (event) {
    if (event.detail.target.id === 'addFormInput') {
        let addFormInput = document.getElementById('addFormInput');
        if (addFormInput.lastElementChild.classList.contains('alert-danger')) {
            setTimeout(function() {
                addFormInput.removeChild(addFormInput.lastChild);
            }, 3000);
        } else {
            setTimeout(function() {
                document.getElementById('addForm').reset();
                addFormInput.removeChild(addFormInput.lastChild);
            }, 1000);
        }
    } else if (event.detail.target.id === 'removeFormInput') {
        setTimeout(function() {
            document.getElementById('removeForm').reset();
            document.getElementById('removeFormInput').removeChild(document.getElementById('removeFormInput').lastChild);
        }, 1000);
    } else if (event.detail.elt.id === 'signInForm') {
        let signInForm = document.getElementById('signInForm');
        if (signInForm.lastElementChild.classList.contains('alert-danger')) {
            setTimeout(function() {
                signInForm.removeChild(signInForm.lastChild);
            }, 3000);
        } else {
            setTimeout(function() {
                htmx.ajax('GET', '/home', { target: '#mainContent', swap: 'innerHTML' });
            }, 1000);
        }
    } else if (event.detail.elt.id === 'signUpForm') {
        let signUpForm = document.getElementById('signUpForm');
        if (signUpForm.lastElementChild.classList.contains('alert-danger')) {
            setTimeout(function() {
                signUpForm.removeChild(signUpForm.lastChild);
            }, 3000);
        } else {
            setTimeout(function() {
                htmx.ajax('GET', '/render/sign-in', { target: '#mainContent', swap: 'innerHTML' });
            }, 1000);
        }
    }
});

let pollingInterval;
document.addEventListener("click", function(event) {
    if (event.target.tagName === "BUTTON" && event.target.classList.contains("btn-light")) {
        const hxGetAttribute = event.target.getAttribute("hx-get");
        if (hxGetAttribute) {
            const feedId = hxGetAttribute.split("/").pop();
            startPolling(feedId);
        } else {
            console.log("hx-get attribute not found on the button");
        }
    } else {
        console.log("Button not clicked");
    }
});

function startPolling(feedId) {
    if (pollingInterval) {
        clearInterval(pollingInterval);
    }
    htmx.ajax('GET', `/render/feed/${feedId}`, { target: '.feed-container-2' });

    pollingInterval = setInterval(function() {
        htmx.ajax('GET', `/render/feed/${feedId}`, { target: '.feed-container-2' });
    }, 60000);
}

document.addEventListener('htmx:afterRequest', function (event) {
    const hxDeleteAttribute = event.detail.elt.getAttribute("hx-delete");
    if (hxDeleteAttribute) {
        let feedContainer2 = document.getElementById('feedContainer2');
        if (feedContainer2.classList.length) {
            feedContainer2.innerHTML = "<h2 class=\"text-muted text-center\">Select a feed to view</h2>";
        }
    } else {
        console.log("hx-delete attribute not found on the element");
    }
});
