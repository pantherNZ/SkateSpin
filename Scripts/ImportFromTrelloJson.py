import sqlite3, json, glob

con = sqlite3.connect("../Assets/StreamingAssets/Database.db")
cur = con.cursor()

cur.execute("DROP TABLE IF EXISTS Tricks_Backup")
cur.execute("CREATE TABLE Tricks_Backup AS SELECT * FROM Tricks")

cur.execute("DELETE from Tricks WHERE SpecialTrick=0")

for file in glob.glob("JSON/*.json"):
    with open(file, 'r') as file:
        parsed_json = json.load(file)

        trick_category = parsed_json['name']

        lists_by_id = {}
        for list in parsed_json["lists"]:
            if list['closed'] == True or not list['name'].isdigit():
                continue
            lists_by_id[list['id']] = list['name']

        for card in parsed_json["cards"]:

            if card['idList'] not in lists_by_id:
                continue

            list_id = lists_by_id[card['idList']]
            trick = card['name']
            category = 'Difficulty'
            category_idx = 3

            cur.execute(f'SELECT * FROM Tricks WHERE Name="{trick}" AND Category="{trick_category}"')

            if cur.fetchone() == None:
                if trick.lower().startswith('fakie '):
                    trick = trick[6:]
                    category = 'FakieDifficulty'
                    category_idx = 4
                elif trick.lower().startswith('switch '):
                    trick = trick[7:]
                    category = 'SwitchDifficulty'
                    category_idx = 5
                elif trick.lower().startswith('nollie '):
                    trick = trick[7:]
                    category = 'NollieDifficulty'
                    category_idx = 6

            cur.execute(f'SELECT * FROM Tricks WHERE Name="{trick}" AND Category="{trick_category}"')

            if cur.fetchone() == None:
                values = f'"{trick}",NULL,"{trick_category}","{list_id}",NULL,NULL,NULL,"0"'
                if category_idx != 3:
                    values = values.split(',')
                    values[3] = 'NULL'
                    values[category_idx] = list_id
                    values = ','.join(values)
                cur.execute(f'INSERT INTO Tricks VALUES ({values})')

                print('[ADDED]' + card['name'] + ' -> ' + list_id)
            else:
                update_sql = f'''
                    UPDATE Tricks
                    SET {category}={list_id}
                    WHERE Name="{trick}" AND Category="{trick_category}"'''
                cur.execute(update_sql)

                print('[UPDATED]' + card['name'] + ' -> ' + list_id)

con.commit()
con.close()