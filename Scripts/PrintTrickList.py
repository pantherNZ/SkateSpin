import sqlite3

con = sqlite3.connect("../Assets/StreamingAssets/Database.db")
cur = con.cursor()

category = 'Mini Ramp'

all_tricks = cur.execute('SELECT * FROM Tricks WHERE Category=?', (category, )).fetchall()

for difficulty in range(1,11):
    print(f'----- Difficulty: {difficulty} ------')
    for trick in all_tricks:
        if trick[3] != None and trick[3] == difficulty:
            print(trick[0])
        if trick[4] != None and trick[4] == difficulty:
            print('Fakie ' + trick[0])
        if trick[5] != None and trick[5] == difficulty:
            print('Switch ' + trick[0])
        if trick[6] != None and trick[6] == difficulty:
            print('Nollie ' + trick[0])

#print(f'----- Difficulty: NULL ------')
#for trick in all_tricks:
#    if trick[3] == None:
#        print(trick[0])
#    if trick[4] == None:
#        print('Fakie ' + trick[0])
#    if trick[5] == None:
#        print('Switch ' + trick[0])
#    if trick[6] == None:
#        print('Nollie ' + trick[0])

con.close()