#!/usr/bin/env dotmad
(letfun is_palindrome (s)
  (do
    (let chars_list (chars s))
    (= chars_list (rev chars_list))))

(let words (list "racecar" "hello" "madam" "dotnet" "level"))
(foreach (lambda (w)
  (if (is_palindrome w)
    (println w " is a palindrome")
    (println w " is not a palindrome")))
  words)
