import csv, sqlite3

con = sqlite3.connect("../Assets/StreamingAssets/Database.db")
cur = con.cursor()

cur.execute("DROP TABLE IF EXISTS Tricks_Backup")
cur.execute("CREATE TABLE Tricks_Backup AS SELECT * FROM Tricks")
cur.execute("DELETE FROM Tricks")

with open('../Tricklist.csv','r') as file:
    # csv.DictReader uses first line in file for column headings by default
    csv_reader = csv.DictReader(file)
    column_names = []

    for row in csv_reader:
        values = ','.join([f'"{x}"' for x in row.values()])
        sql = f"INSERT INTO Tricks VALUES ({values})"
        #print(sql)
        cur.execute(sql)

        if len(column_names) == 0:
            column_names = row.keys()

    for column in column_names:
        sql = f"UPDATE Tricks SET {column} = NULL WHERE {column} = 'INVALID'"
        #print(sql)
        cur.execute(sql)

con.commit()
con.close()