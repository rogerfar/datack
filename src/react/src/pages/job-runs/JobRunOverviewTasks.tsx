import {
    CheckIcon,
    TriangleDownIcon,
    TriangleUpIcon,
    WarningIcon
} from '@chakra-ui/icons';
import { Th, Thead } from '@chakra-ui/react';
import { chakra } from '@chakra-ui/system';
import { Table, Tbody, Td, Tr } from '@chakra-ui/table';
import { format, formatDistanceStrict } from 'date-fns';
import React, { FC } from 'react';
import { Column, useSortBy, useTable } from 'react-table';
import { JobRunTask } from '../../models/job-run-task';

type Props = {
    jobRunTasks: JobRunTask[];
    onRowClick: (jobRunTaskId: string) => void;
};

const JobRunOverviewTasks: FC<Props> = (props) => {
    const { jobRunTasks, onRowClick } = props;

    const columns = React.useMemo(() => {
        const columns: Column<JobRunTask>[] = [
            {
                Header: 'Started',
                accessor: 'started',
                sortType: 'datetime',
                Cell: ({ cell: { value } }) => {
                    if (value == null) {
                        return '';
                    }
                    return format(value, 'HH:mm:ss');
                },
            },
            {
                Header: 'Completed',
                accessor: 'completed',
                sortType: 'datetime',
                Cell: ({ cell: { value } }) => {
                    if (!value) {
                        return '';
                    }
                    return format(value, 'HH:mm:ss');
                },
            },
            {
                Header: 'Runtime',
                accessor: 'runTime',
                Cell: ({ cell: { value } }) => {
                    if (value == null) {
                        return '';
                    }
                    return formatDistanceStrict(0, value * 1000);
                },
            },
            {
                Header: 'Task',
                accessor: 'type',
            },
            {
                Header: 'Item',
                accessor: 'itemName',
            },
            {
                Header: 'Result',
                accessor: 'isError',
                Cell: ({ cell: { value } }) => {
                    if (value) {
                        return <WarningIcon style={{ color: 'red' }} />;
                    }
                    return <CheckIcon style={{ color: 'green' }} />;
                },
            },
        ];
        return columns;
    }, []);

    const { getTableProps, getTableBodyProps, headerGroups, rows, prepareRow } =
        useTable<JobRunTask>({ columns, data: jobRunTasks }, useSortBy);

    return (
        <Table {...getTableProps()} style={{ width: 'auto' }} size="sm">
            <Thead>
                {headerGroups.map((headerGroup) => (
                    <Tr {...headerGroup.getHeaderGroupProps()}>
                        {headerGroup.headers.map((column) => (
                            <Th
                                {...column.getHeaderProps(
                                    column.getSortByToggleProps()
                                )}
                            >
                                {column.render('Header')}
                                <chakra.span pl="4">
                                    {column.isSorted ? (
                                        column.isSortedDesc ? (
                                            <TriangleDownIcon aria-label="sorted descending" />
                                        ) : (
                                            <TriangleUpIcon aria-label="sorted ascending" />
                                        )
                                    ) : null}
                                </chakra.span>
                            </Th>
                        ))}
                    </Tr>
                ))}
            </Thead>
            <Tbody {...getTableBodyProps()}>
                {rows.map((row) => {
                    prepareRow(row);
                    return (
                        <Tr
                            {...row.getRowProps()}
                            onClick={() =>
                                onRowClick(row.original.jobRunTaskId)
                            }
                            style={{ cursor: 'pointer' }}
                        >
                            {row.cells.map((cell) => (
                                <Td {...cell.getCellProps()}>
                                    {cell.render('Cell')}
                                </Td>
                            ))}
                        </Tr>
                    );
                })}
            </Tbody>
        </Table>
    );
};

export default JobRunOverviewTasks;