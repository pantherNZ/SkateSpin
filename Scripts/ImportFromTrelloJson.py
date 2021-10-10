import sqlite3, json

con = sqlite3.connect("../Assets/StreamingAssets/Database.db")
cur = con.cursor()

category = 'Flat Ground'

cur.execute("DROP TABLE IF EXISTS Tricks_Backup")
cur.execute("CREATE TABLE Tricks_Backup AS SELECT * FROM Tricks")

with open('TrelloJson.json', 'r') as file:
    parsed_json = json.load(file)

    lists_by_id = {}
    for list in parsed_json["lists"]:
        lists_by_id[list['id']] = list['name']

    for card in parsed_json["cards"]:
        list_id = card['idList']
        trick = card['name']
        category = 'Difficulty'

        if trick.startswith('Fakie '):
            trick = trick[6:]
            category = 'FakieDifficulty'
        elif trick.startswith('Switch '):
                    trick = trick[7:]
                    category = 'SwitchDifficulty'
        elif trick.startswith('Nollie '):
                    trick = trick[7:]
                    category = 'NollieDifficulty'

        update_sql = f'''
            UPDATE Tricks
            SET {category}={list_id}
            WHERE Name={trick};'''
        cur.execute(update_sql)

        print(card['name'] + ' -> ' + list_id)

con.commit()
con.close()