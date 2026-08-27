#!/usr/bin/env dotmad
(letfun make_person (name age job)
  (record
    (name name)
    (age age)
    (job job)))

(letfun print_person (person)
  (println (. person name) " is " (. person age) " and works as " (. person job)))

(let people
  (list
    (make_person "Alice" 30 "Engineer")
    (make_person "Bob" 25 "Designer")
    (make_person "Charlie" 35 "Manager")))

(foreach print_person people)
