export function focusElement(elementId) {
  const element = document.getElementById(elementId);

  if (element) {
    element.focus();
  }
}